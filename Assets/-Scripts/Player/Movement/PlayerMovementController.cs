using System;
using UnityEngine;

namespace UGG.Move
{
    public class PlayerMovementController : CharacterMovementBase
    {
        //引用
        private Transform characterCamera;
        private TP_CameraController _tpCameraController;
        private UGG.Combat.PlayerCombatSystem _playerCombatSystem;
        private UGG.Combat.HighStressSkillController _highStressSkillController;

        [SerializeField, Header("相机站立锁定点")] private Transform standCameraLook;
        [SerializeField, Header("相机下蹲锁定点")] private Transform crouchCameraLook;

        //Ref Value
        private float targetRotation;
        private float rotationVelocity;
        private Vector3 currentDodgeDirection = Vector3.back;
        private float nextDodgeTime;

        //LerpTime
        [SerializeField, Header("旋转速度")] private float rotationLerpTime;
        [SerializeField, Header("旋转方向的平滑时间")] private float moveDirctionSlerpTime;


        //Move Speed
        [SerializeField, Header("行走速度")] private float walkSpeed;
        [SerializeField, Header("奔跑速度")] private float runSpeed;
        [SerializeField, Header("下蹲速度")] private float crouchMoveSpeed;

        [SerializeField, Header("动画移动速度倍率")] private float animationMoveSpeedMult;
        [SerializeField, Header("受击提前恢复输入时间(0-1)")] [Range(0f, 1f)] private float hitRecoverNormalizedTime = 0.45f;
        [SerializeField, Header("受击后提前恢复闪避时间(0-1)")] [Range(0f, 1f)] private float hitDodgeRecoverNormalizedTime = 0.28f;
        [SerializeField, Header("短闪移动速度"), Range(1f, 20f)] private float dodgeMoveSpeed = 7.5f;
        [SerializeField, Header("短闪冷却时间"), Range(0f, 1.5f)] private float dodgeCooldown = 0.35f;
        [SerializeField, Header("短闪动画速度"), Range(0.5f, 3f)] private float dodgeAnimationSpeed = 1.25f;
        [SerializeField, Header("短闪位移结束时间(0-1)"), Range(0.1f, 1f)] private float dodgeMoveEndNormalizedTime = 0.72f;
        [SerializeField, Header("短闪无敌开始时间(0-1)"), Range(0f, 1f)] private float dodgeInvincibleStartNormalizedTime = 0.08f;
        [SerializeField, Header("短闪无敌结束时间(0-1)"), Range(0f, 1f)] private float dodgeInvincibleEndNormalizedTime = 0.55f;


        [SerializeField, Header("角色胶囊控制(下蹲)")] private Vector3 crouchCenter;
        [SerializeField] private Vector3 originCenter;
        [SerializeField] private Vector3 cameraLookPositionOnCrouch;
        [SerializeField] private Vector3 cameraLookPositionOrigin;
        [SerializeField] private float crouchHeight;
        [SerializeField] private float originHeight;
        [SerializeField] private bool isOnCrouch;
        [SerializeField] private Transform crouchDetectionPosition;
        [SerializeField] private Transform CameraLook;
        [SerializeField] private LayerMask crouchDetectionLayer;

        //AnimationID
        private int crouchID = Animator.StringToHash("Crouch");

        #region 内部函数

        protected override void Awake()
        {
            base.Awake();

            characterCamera = Camera.main.transform.root.transform;
            _tpCameraController = characterCamera.GetComponent<TP_CameraController>();
            _playerCombatSystem = GetComponentInChildren<UGG.Combat.PlayerCombatSystem>();
            _highStressSkillController = GetComponent<UGG.Combat.HighStressSkillController>();
        }

        protected override void Start()
        {
            base.Start();

            cameraLookPositionOrigin = CameraLook.position;
        }

        protected override void Update()
        {
            base.Update();
            
            PlayerMoveDirection();
            UpdateRollAnimation();
        }

        private void LateUpdate()
        {
            CharacterCrouchControl();
            UpdateMotionAnimation();
            UpdateCrouchAnimation();
            UpdateRollAnimation();
        }

        #endregion

        #region 条件

        private bool CanMoveContro()
        {
            if (IsHighStressSkillLocked()) return false;

            bool canMoveOnGround = isOnGround && characterAnimator.CheckAnimationTag("Motion");
            bool canMoveOnCrouch = characterAnimator.CheckAnimationTag("CrouchMotion");
            bool canRecoverFromHit = isOnGround &&
                                     characterAnimator.CheckCurrentTagAnimationTimeIsExceed("Hit", hitRecoverNormalizedTime);
            bool canRecoverFromFinalAttack = isOnGround &&
                                             _playerCombatSystem != null &&
                                             _playerCombatSystem.CanRecoverMovementFromFinalAttack();

            return canMoveOnGround || canMoveOnCrouch || canRecoverFromHit || canRecoverFromFinalAttack;
        }

        private bool CanCrouch()
        {
            if (IsHighStressSkillLocked()) return false;
            if (characterAnimator.CheckAnimationTag("Crouch")) return false;
            if (characterAnimator.GetFloat(runID)>.9f) return false;
            
            return true;
        }
        
        private bool CanRunControl()
        {
            if (Vector3.Dot(movementDirection.normalized, transform.forward) < 0.75f) return false;
            if (!CanMoveContro()) return false;
          
            return true;
        }

        #endregion
        
        private void PlayerMoveDirection()
        {
            //如果在地面 并且按键没有输入 就把移动方向设置为0
            if (isOnGround && _inputSystem.playerMovement == Vector2.zero)
                movementDirection = Vector3.zero;
            
            //如果可以移动
            if(CanMoveContro()) 
            {
                //如果玩家输入不为0
                if (_inputSystem.playerMovement != Vector2.zero)
                {
                    //将玩家X输入轴和Y输入轴的弧度转化为角度并加上摄像机自身Y轴的旋转 使摄像机跟着角色旋转
                    targetRotation = Mathf.Atan2(_inputSystem.playerMovement.x, _inputSystem.playerMovement.y) * Mathf.Rad2Deg + characterCamera.localEulerAngles.y;

                    //玩家旋转平滑
                    transform.eulerAngles = Vector3.up * Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, rotationLerpTime);

                    //旋转并始终向Z轴移动的向量
                    var direction = Quaternion.Euler(0f, targetRotation, 0f) * Vector3.forward;

                    //将移动方向标准化
                    direction = direction.normalized;

                    //优化：让角色突然换方向更流畅
                    movementDirection = Vector3.Slerp(movementDirection, ResetMoveDirectionOnSlop(direction), moveDirctionSlerpTime * Time.deltaTime);
                }
            }
            else 
            {
                //如果不能移动 则把移动向量设置为0
                movementDirection = Vector3.zero;
            }

            //玩家移动
            //优化：先乘浮点再对向量进行运算可以增加运算速度
            control.Move((characterCurrentMoveSpeed * Time.deltaTime) * movementDirection.normalized + Time.deltaTime * new Vector3(0.0f, verticalSpeed, 0.0f));
            //control.Move(movementDirection.normalized * (characterCurrentMoveSpeed * Time.deltaTime) + new Vector3(0.0f, verticalSpeed, 0.0f) * Time.deltaTime);
        }

        private void UpdateMotionAnimation()
        {
            //如果可以奔跑
            if (CanRunControl())
            {
                characterAnimator.SetFloat(movementID, _inputSystem.playerMovement.sqrMagnitude * ((_inputSystem.playerRun && !isOnCrouch) ? 2f : 1f), 0.1f, Time.deltaTime);

                //如果玩家按下奔跑键并且不是下蹲状态就把角色当前移动速度设置为奔跑速度 如果没有按下奔跑键并且不是下蹲状态则设置为行走速度
                characterCurrentMoveSpeed = (_inputSystem.playerRun && !isOnCrouch) ? runSpeed : walkSpeed;
            }
            else
            {
                characterAnimator.SetFloat(movementID, 0f, 0.05f, Time.deltaTime);
                characterCurrentMoveSpeed = 0f;
            }

            characterAnimator.SetFloat(runID, (_inputSystem.playerRun && !isOnCrouch) ? 1f : 0f);
        }

        private void UpdateCrouchAnimation()
        {
            //如果角色处于下蹲状态
            if (isOnCrouch)
            {
                //将角色当前移动速度设置为下蹲时的移动速度
                characterCurrentMoveSpeed = crouchMoveSpeed;
            }
        }

        private void UpdateRollAnimation()
        {
            if (IsHighStressSkillLocked())
            {
                return;
            }

            //如果玩家按下翻滚键
            if (_inputSystem.playerRoll && CanStartDodge())
            {
                CacheDodgeDirection();
                //直接进入普通武器短闪状态，避免依赖SecondaryWeapon分支
                characterAnimator.Play("Roll_B", 0, 0f);
                characterAnimator.speed = dodgeAnimationSpeed;
                nextDodgeTime = Time.time + dodgeCooldown;
            }

            //检测是否在翻滚状态
            if (characterAnimator.CheckAnimationTag("Roll"))
            {
                if (characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < dodgeMoveEndNormalizedTime)
                {
                    CharacterMoveInterface(currentDodgeDirection, dodgeMoveSpeed, true);
                }
            }
            else if (characterAnimator.speed != 1f)
            {
                characterAnimator.speed = 1f;
            }
        }

        private bool CanStartDodge()
        {
            if (Time.time < nextDodgeTime) return false;
            if (characterAnimator.CheckAnimationTag("Roll")) return false;
            if (!CanDodgeFromCurrentState()) return false;
            if (IsInDodgeLockedState()) return false;

            return true;
        }

        private bool CanDodgeFromCurrentState()
        {
            bool canDodgeFromGroundMotion = isOnGround && characterAnimator.CheckAnimationTag("Motion");
            bool canDodgeFromCrouch = characterAnimator.CheckAnimationTag("CrouchMotion");
            bool canDodgeFromRecoveredHit = isOnGround &&
                                           characterAnimator.CheckCurrentTagAnimationTimeIsExceed("Hit", hitDodgeRecoverNormalizedTime);
            bool canDodgeFromRecoveredAttack = isOnGround &&
                                              _playerCombatSystem != null &&
                                              _playerCombatSystem.CanRecoverFromAttack();
            bool canDodgeFromRecoveredParry = isOnGround &&
                                             _playerCombatSystem != null &&
                                             _playerCombatSystem.CanRecoverFromParryForDodge();
            bool canDodgeFromFinalAttack = isOnGround &&
                                           _playerCombatSystem != null &&
                                           _playerCombatSystem.CanRecoverMovementFromFinalAttack();

            return canDodgeFromGroundMotion ||
                   canDodgeFromCrouch ||
                   canDodgeFromRecoveredHit ||
                   canDodgeFromRecoveredAttack ||
                   canDodgeFromRecoveredParry ||
                   canDodgeFromFinalAttack;
        }

        private bool IsInDodgeLockedState()
        {
            bool isEarlyHit = characterAnimator.CheckAnimationTag("Hit") &&
                              !characterAnimator.CheckCurrentTagAnimationTimeIsExceed("Hit", hitDodgeRecoverNormalizedTime);
            bool isTransitioningToHit = characterAnimator.IsInTransition(0) &&
                                        characterAnimator.GetNextAnimatorStateInfo(0).IsTag("Hit") &&
                                        !characterAnimator.CheckCurrentTagAnimationTimeIsExceed("Hit", hitDodgeRecoverNormalizedTime);

            if (isEarlyHit) return true;
            if (isTransitioningToHit) return true;
            if (characterAnimator.CheckAnimationTag("ParryHit")) return true;
            if (IsDownHitState(characterAnimator.GetCurrentAnimatorStateInfo(0))) return true;
            if (characterAnimator.IsInTransition(0) && IsDownHitState(characterAnimator.GetNextAnimatorStateInfo(0))) return true;
            if (characterAnimator.CheckAnimationTag("Death")) return true;

            return false;
        }

        private static bool IsDownHitState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.IsName("Hit_D_Up") ||
                   stateInfo.IsName("Hit_D_Left") ||
                   stateInfo.IsName("Hit_D_Right");
        }

        public bool IsDodgeInvulnerable()
        {
            if (!characterAnimator.CheckAnimationTag("Roll"))
            {
                return false;
            }

            float normalizedTime = characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            return normalizedTime >= dodgeInvincibleStartNormalizedTime &&
                   normalizedTime <= dodgeInvincibleEndNormalizedTime;
        }

        private void CacheDodgeDirection()
        {
            Vector2 input = _inputSystem.playerMovement;
            Vector3 cameraForward = characterCamera.forward;
            Vector3 cameraRight = characterCamera.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            if (input == Vector2.zero)
            {
                characterAnimator.SetFloat(horizontalID, 0f);
                characterAnimator.SetFloat(verticalID, -1f);
                currentDodgeDirection = -transform.forward;
                return;
            }

            Vector2 snappedInput;

            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                snappedInput = new Vector2(Mathf.Sign(input.x), 0f);
            }
            else
            {
                snappedInput = new Vector2(0f, Mathf.Sign(input.y));
            }

            characterAnimator.SetFloat(horizontalID, snappedInput.x);
            characterAnimator.SetFloat(verticalID, snappedInput.y);

            currentDodgeDirection = cameraRight * snappedInput.x + cameraForward * snappedInput.y;
            currentDodgeDirection = currentDodgeDirection.sqrMagnitude > 0f
                ? currentDodgeDirection.normalized
                : -transform.forward;
        }
        
        private void CharacterCrouchControl()
        {
            //如果角色不能下蹲 则返回
            if (!CanCrouch()) return;

            //如果角色按下蹲键
            if (_inputSystem.playerCrouch)
            {
                //判断现在是否处于下蹲状态
                if (isOnCrouch)
                {
                    //检测头顶是否有障碍物 取反
                    if (!DetectionHeadHasObject())
                    {
                        isOnCrouch = false;
                        characterAnimator.SetFloat(crouchID, 0f);
                        SetCrouchColliderHeight(originHeight, originCenter);
                        _tpCameraController.SetLookPlayerTarget(standCameraLook);
                    }
                }
                else
                {
                    isOnCrouch = true;
                    characterAnimator.SetFloat(crouchID, 1f);
                    SetCrouchColliderHeight(crouchHeight, crouchCenter);
                    _tpCameraController.SetLookPlayerTarget(crouchCameraLook);
                }
            }
        }
        
        private void SetCrouchColliderHeight(float height,Vector3 center)
        {
            control.center = center;
            control.height = height;
        }
        
        /// <summary>
        /// 检测头顶是否有障碍物
        /// </summary>
        /// <returns></returns>
        private bool DetectionHeadHasObject()
        {
            Collider[] hasObjects = new Collider[1];

            int objectCount = Physics.OverlapSphereNonAlloc(crouchDetectionPosition.position, 0.5f, hasObjects, crouchDetectionLayer);
            
            if (objectCount > 0)
            {
                return true;
            }

            return false;
        }

        private bool IsHighStressSkillLocked()
        {
            return _highStressSkillController != null && _highStressSkillController.BlocksStandardActions;
        }
    }
}
