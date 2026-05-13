using UnityEngine;

public class AI2GreatSwordPresentation : MonoBehaviour
{
    [SerializeField] private Transform hipGS;
    [SerializeField] private Transform handGS;
    [SerializeField] private Transform handKatana;

    private void Awake()
    {
        ApplyPresentation();
    }

    private void LateUpdate()
    {
        ApplyPresentation();
    }

    private void ApplyPresentation()
    {
        if (handGS != null)
        {
            handGS.gameObject.SetActive(true);
        }

        if (hipGS != null && hipGS != handGS)
        {
            hipGS.gameObject.SetActive(false);
        }

        if (handKatana != null)
        {
            handKatana.gameObject.SetActive(false);
        }
    }
}
