using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterInputSystem : MonoBehaviour
{
    private InputController _inputController;

    private bool IsGameplayInputBlocked => FirstPickupDialogueController.IsBlockingPauseMenu;

    //Key Setting
    public Vector2 playerMovement
    {
        get => IsGameplayInputBlocked ? Vector2.zero : _inputController.PlayerInput.Movement.ReadValue<Vector2>();
    }

    public Vector2 cameraLook
    {
        get => IsGameplayInputBlocked ? Vector2.zero : _inputController.PlayerInput.CameraLook.ReadValue<Vector2>();
    }

    public bool playerLAtk
    {
        get => !IsGameplayInputBlocked &&
               !FirstPickupDialogueController.IsBlockingAttackInput &&
               _inputController.PlayerInput.LAtk.triggered;
    }
    
    public bool playerRAtk
    {
        get => !IsGameplayInputBlocked &&
               _inputController.PlayerInput.RAtk.phase == InputActionPhase.Performed;
    }
    public bool playerDefen
    {
        get => !IsGameplayInputBlocked &&
               _inputController.PlayerInput.Defen.phase == InputActionPhase.Performed;
    }

    public bool playerRun
    {
        get => !IsGameplayInputBlocked &&
               _inputController.PlayerInput.Run.phase == InputActionPhase.Performed;
    }

    public bool playerRoll
    {
        get => !IsGameplayInputBlocked &&
               _inputController.PlayerInput.Roll.triggered;
    }

    public bool playerCrouch
    {
        get => !IsGameplayInputBlocked &&
               _inputController.PlayerInput.Crouch.triggered;
    }

    //内部函数
    private void Awake()
    {
        if (_inputController == null)
            _inputController = new InputController();
    }

    private void OnEnable()
    {
        _inputController.Enable();
    }

    private void OnDisable()
    {
        _inputController.Disable();
    }
}
