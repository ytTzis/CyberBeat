using UnityEngine;

[DisallowMultipleComponent]
public class PickupHighlight : MonoBehaviour
{
    private enum HighlightColorMode
    {
        AutoFromName,
        Yellow,
        Red,
        Blue,
        Custom
    }

    [SerializeField, Header("Detection")] private string playerTag = "Player";
    [SerializeField, Min(0.1f)] private float nearBoostRadius = 4f;

    [SerializeField, Header("Object Highlight")] private bool enableObjectHighlight;
    [SerializeField, Header("Highlight")] private HighlightColorMode colorMode = HighlightColorMode.Yellow;
    [SerializeField] private Color customHighlightColor = new Color(1f, 0.92f, 0.28f, 1f);
    [SerializeField, Min(0f)] private float minEmissionIntensity = 5.6f;
    [SerializeField, Min(0f)] private float maxEmissionIntensity = 8.8f;
    [SerializeField, Min(0f)] private float nearEmissionBonus = 4.2f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 1.65f;
    [SerializeField] private bool tintBaseColorSlightly = true;
    [SerializeField, Range(0f, 1f)] private float baseColorTintStrength = 0.1f;

    [SerializeField, Header("Motion")] private bool enableFloatMotion = true;
    [SerializeField, Min(0f)] private float floatAmplitude = 0.12f;
    [SerializeField, Min(0.01f)] private float floatSpeed = 1.8f;
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeed = 35f;

    [SerializeField, Header("Indicator")] private bool enableIndicator = true;
    [SerializeField, Min(0f)] private float indicatorHeight = 0.58f;
    [SerializeField, Min(0.01f)] private float indicatorWidth = 0.14f;
    [SerializeField, Min(0.01f)] private float indicatorLength = 0.14f;
    [SerializeField, Min(0.01f)] private float indicatorDepth = 0.22f;
    [SerializeField, Min(0f)] private float indicatorFloatAmplitude = 0.05f;
    [SerializeField, Min(0.01f)] private float indicatorFloatSpeed = 2.1f;
    [SerializeField] private float indicatorRotationSpeed = 140f;
    [SerializeField, Range(0f, 1f)] private float indicatorAlpha = 1f;

    [SerializeField, Header("Debug")] private bool showDebugGizmos;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material cachedIndicatorMaterial;
    private static Mesh cachedIndicatorMesh;

    private Renderer[] cachedRenderers;
    private Color[] originalBaseColors;
    private bool[] usesBaseColorProperty;
    private bool[] usesColorProperty;
    private bool[] usesEmissionProperty;
    private MaterialPropertyBlock propertyBlock;
    private MaterialPropertyBlock indicatorPropertyBlock;
    private Transform playerTarget;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Transform indicatorTransform;
    private MeshRenderer indicatorRenderer;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        indicatorPropertyBlock = new MaterialPropertyBlock();
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        CacheRenderers();
        playerTarget = FindPlayerTarget();
        EnsureIndicator();
    }

    private void OnEnable()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (indicatorPropertyBlock == null)
        {
            indicatorPropertyBlock = new MaterialPropertyBlock();
        }

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            CacheRenderers();
        }

        EnsureIndicator();
    }

    private void Update()
    {
        if (playerTarget == null)
        {
            playerTarget = FindPlayerTarget();
        }

        float distanceFactor = GetPlayerDistanceFactor();
        if (enableObjectHighlight)
        {
            ApplyHighlight(distanceFactor);
        }
        else
        {
            ClearHighlight();
        }

        ApplyMotion();
        UpdateIndicator(distanceFactor);
    }

    private void OnDisable()
    {
        ClearHighlight();
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
    }

    private void OnValidate()
    {
        nearBoostRadius = Mathf.Max(0.1f, nearBoostRadius);
        pulseSpeed = Mathf.Max(0.01f, pulseSpeed);
        floatSpeed = Mathf.Max(0.01f, floatSpeed);
        rotationAxis = rotationAxis == Vector3.zero ? Vector3.up : rotationAxis.normalized;
        maxEmissionIntensity = Mathf.Max(minEmissionIntensity, maxEmissionIntensity);
        indicatorWidth = Mathf.Max(0.01f, indicatorWidth);
        indicatorLength = Mathf.Max(0.01f, indicatorLength);
        indicatorDepth = Mathf.Max(0.01f, indicatorDepth);
        indicatorFloatSpeed = Mathf.Max(0.01f, indicatorFloatSpeed);
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        originalBaseColors = new Color[cachedRenderers.Length];
        usesBaseColorProperty = new bool[cachedRenderers.Length];
        usesColorProperty = new bool[cachedRenderers.Length];
        usesEmissionProperty = new bool[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer currentRenderer = cachedRenderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            Material sharedMaterial = currentRenderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                continue;
            }

            usesEmissionProperty[i] = sharedMaterial.HasProperty(EmissionColorId);
            usesBaseColorProperty[i] = sharedMaterial.HasProperty(BaseColorId);
            usesColorProperty[i] = sharedMaterial.HasProperty(ColorId);

            if (usesBaseColorProperty[i])
            {
                originalBaseColors[i] = sharedMaterial.GetColor(BaseColorId);
            }
            else if (usesColorProperty[i])
            {
                originalBaseColors[i] = sharedMaterial.GetColor(ColorId);
            }
            else
            {
                originalBaseColors[i] = Color.white;
            }
        }
    }

    private float GetPlayerDistanceFactor()
    {
        if (playerTarget == null)
        {
            return 0f;
        }

        float sqrDistance = (playerTarget.position - transform.position).sqrMagnitude;
        float sqrRadius = nearBoostRadius * nearBoostRadius;
        if (sqrDistance >= sqrRadius)
        {
            return 0f;
        }

        return 1f - Mathf.Clamp01(sqrDistance / sqrRadius);
    }

    private void ApplyHighlight(float distanceFactor)
    {
        Color highlightColor = ResolveHighlightColor();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
        float intensity = Mathf.Lerp(minEmissionIntensity, maxEmissionIntensity, pulse) + nearEmissionBonus * distanceFactor;
        Color emissionColor = highlightColor * intensity;
        Color tintedBaseColor = Color.Lerp(Color.white, highlightColor, baseColorTintStrength * Mathf.Clamp01(0.35f + distanceFactor));

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer currentRenderer = cachedRenderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            currentRenderer.GetPropertyBlock(propertyBlock);

            if (usesEmissionProperty[i])
            {
                propertyBlock.SetColor(EmissionColorId, emissionColor);
            }

            if (tintBaseColorSlightly)
            {
                Color targetBaseColor = MultiplyRgb(originalBaseColors[i], tintedBaseColor);
                if (usesBaseColorProperty[i])
                {
                    propertyBlock.SetColor(BaseColorId, targetBaseColor);
                }
                else if (usesColorProperty[i])
                {
                    propertyBlock.SetColor(ColorId, targetBaseColor);
                }
            }

            currentRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplyMotion()
    {
        Vector3 localPosition = initialLocalPosition;
        if (enableFloatMotion && floatAmplitude > 0f)
        {
            localPosition.y += Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        }

        transform.localPosition = localPosition;

        if (enableRotation && rotationAxis != Vector3.zero && rotationSpeed != 0f)
        {
            transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void EnsureIndicator()
    {
        if (!enableIndicator)
        {
            if (indicatorTransform != null)
            {
                indicatorTransform.gameObject.SetActive(false);
            }

            return;
        }

        if (indicatorTransform == null)
        {
            Transform existingChild = transform.Find("PickupHighlightIndicator");
            if (existingChild != null)
            {
                indicatorTransform = existingChild;
                indicatorRenderer = existingChild.GetComponent<MeshRenderer>();
            }
        }

        if (indicatorTransform == null)
        {
            GameObject indicatorObject = new GameObject("PickupHighlightIndicator");
            indicatorObject.transform.SetParent(transform, false);
            indicatorTransform = indicatorObject.transform;

            MeshFilter meshFilter = indicatorObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetIndicatorMesh();

            indicatorRenderer = indicatorObject.AddComponent<MeshRenderer>();
            indicatorRenderer.sharedMaterial = GetIndicatorMaterial();
            indicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            indicatorRenderer.receiveShadows = false;
            indicatorRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            indicatorRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        indicatorTransform.gameObject.SetActive(true);
        indicatorTransform.localScale = new Vector3(indicatorWidth, indicatorDepth, indicatorLength);
    }

    private void UpdateIndicator(float distanceFactor)
    {
        if (!enableIndicator || indicatorTransform == null || indicatorRenderer == null)
        {
            return;
        }

        float bobOffset = indicatorFloatAmplitude > 0f
            ? Mathf.Sin(Time.time * indicatorFloatSpeed) * indicatorFloatAmplitude
            : 0f;

        indicatorTransform.localPosition = new Vector3(0f, indicatorHeight + bobOffset, 0f);
        indicatorTransform.localRotation = Quaternion.Euler(0f, Time.time * indicatorRotationSpeed, 0f);
        indicatorTransform.localScale = new Vector3(indicatorWidth, indicatorDepth, indicatorLength);

        Color indicatorColor = GetIndicatorColor(distanceFactor);
        indicatorRenderer.GetPropertyBlock(indicatorPropertyBlock);
        indicatorPropertyBlock.SetColor("_Color", indicatorColor);
        indicatorPropertyBlock.SetColor("_BaseColor", indicatorColor);
        indicatorRenderer.SetPropertyBlock(indicatorPropertyBlock);
    }

    private Color GetIndicatorColor(float distanceFactor)
    {
        Color gold = new Color(1f, 0.88f, 0.22f, indicatorAlpha);
        Color warmCore = new Color(1f, 0.97f, 0.7f, indicatorAlpha);
        return Color.Lerp(gold, warmCore, Mathf.Clamp01(0.25f + distanceFactor * 0.35f));
    }

    private static Material GetIndicatorMaterial()
    {
        if (cachedIndicatorMaterial != null)
        {
            return cachedIndicatorMaterial;
        }

        Shader indicatorShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (indicatorShader == null)
        {
            indicatorShader = Shader.Find("Unlit/Color");
        }

        if (indicatorShader == null)
        {
            indicatorShader = Shader.Find("Sprites/Default");
        }

        if (indicatorShader == null)
        {
            return null;
        }

        cachedIndicatorMaterial = new Material(indicatorShader)
        {
            name = "PickupHighlightIndicatorMaterial"
        };

        if (cachedIndicatorMaterial.HasProperty("_BaseColor"))
        {
            cachedIndicatorMaterial.SetColor("_BaseColor", new Color(1f, 0.9f, 0.22f, 1f));
        }

        if (cachedIndicatorMaterial.HasProperty("_Color"))
        {
            cachedIndicatorMaterial.SetColor("_Color", new Color(1f, 0.9f, 0.22f, 1f));
        }

        if (cachedIndicatorMaterial.HasProperty("_Surface"))
        {
            cachedIndicatorMaterial.SetFloat("_Surface", 0f);
        }

        if (cachedIndicatorMaterial.HasProperty("_Blend"))
        {
            cachedIndicatorMaterial.SetFloat("_Blend", 0f);
        }

        if (cachedIndicatorMaterial.HasProperty("_Cull"))
        {
            cachedIndicatorMaterial.SetFloat("_Cull", 0f);
        }

        if (cachedIndicatorMaterial.HasProperty("_ZWrite"))
        {
            cachedIndicatorMaterial.SetFloat("_ZWrite", 1f);
        }

        return cachedIndicatorMaterial;
    }

    private static Mesh GetIndicatorMesh()
    {
        if (cachedIndicatorMesh != null)
        {
            return cachedIndicatorMesh;
        }

        Vector3 topA = new Vector3(-0.5f, 0f, -0.288675f);
        Vector3 topB = new Vector3(0.5f, 0f, -0.288675f);
        Vector3 topC = new Vector3(0f, 0f, 0.57735f);
        Vector3 bottom = new Vector3(0f, -1f, 0f);

        Vector3[] vertices =
        {
            topA, topB, topC,
            topA, bottom, topB,
            topB, bottom, topC,
            topC, bottom, topA,
            topC, topB, topA,
            topB, bottom, topA,
            topC, bottom, topB,
            topA, bottom, topC
        };

        int[] triangles = new int[vertices.Length];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = i;
        }

        cachedIndicatorMesh = new Mesh
        {
            name = "PickupHighlightIndicatorMesh"
        };
        cachedIndicatorMesh.vertices = vertices;
        cachedIndicatorMesh.triangles = triangles;
        cachedIndicatorMesh.RecalculateNormals();
        cachedIndicatorMesh.RecalculateBounds();
        return cachedIndicatorMesh;
    }

    private void ClearHighlight()
    {
        if (cachedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer currentRenderer = cachedRenderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            currentRenderer.SetPropertyBlock(null);
        }
    }

    private Transform FindPlayerTarget()
    {
        CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
        if (inputSystem != null)
        {
            return inputSystem.transform;
        }

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (taggedPlayer != null)
            {
                return taggedPlayer.transform;
            }
        }

        GameObject namedPlayer = GameObject.Find("Player");
        if (namedPlayer != null)
        {
            return namedPlayer.transform;
        }

        GameObject fallbackPlayer = GameObject.Find("Player (1)");
        if (fallbackPlayer != null)
        {
            return fallbackPlayer.transform;
        }

        return null;
    }

    private Color ResolveHighlightColor()
    {
        switch (colorMode)
        {
            case HighlightColorMode.Red:
                return new Color(1f, 0.22f, 0.16f, 1f);
            case HighlightColorMode.Blue:
                return new Color(0.22f, 0.62f, 1f, 1f);
            case HighlightColorMode.Yellow:
                return new Color(1f, 0.92f, 0.28f, 1f);
            case HighlightColorMode.Custom:
                return customHighlightColor;
            default:
                return ResolveAutoColor();
        }
    }

    private Color ResolveAutoColor()
    {
        string lowerName = name.ToLowerInvariant();
        if (lowerName.Contains("red"))
        {
            return new Color(1f, 0.22f, 0.16f, 1f);
        }

        if (lowerName.Contains("blue"))
        {
            return new Color(0.22f, 0.62f, 1f, 1f);
        }

        return customHighlightColor;
    }

    private static Color MultiplyRgb(Color source, Color tint)
    {
        return new Color(
            source.r * tint.r,
            source.g * tint.g,
            source.b * tint.b,
            source.a);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = ResolveHighlightColor();
        Gizmos.DrawWireSphere(transform.position, nearBoostRadius);
    }
}
