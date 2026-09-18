using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Minimo/Character Drag Rotate")]
public class MinimoCharacterDragRotate : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private Camera inputCamera;
    [SerializeField] private float dragActivationPixels = 12f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 0.22f;
    [SerializeField] private bool invert = false;
    [SerializeField] private bool requirePointerOnCharacter = true;
    [SerializeField] private bool autoRotate;
    [SerializeField] private float autoRotateDegreesPerSecond = 32f;

    [Header("Inertia")]
    [SerializeField] private bool useInertia = true;
    [SerializeField] private float inertiaDamping = 7.5f;

    private Camera mainCamera;
    private bool dragging;
    private int activeTouchId = -1;
    private Vector2 lastPointerPosition;
    private Vector2 pointerDownPosition;
    private bool pendingMouseDrag;
    private float yawVelocity;

    public bool IsPointerCaptured => dragging || pendingMouseDrag || activeTouchId >= 0;
    public float DragActivationPixels => Mathf.Max(0f, dragActivationPixels);
    public bool IsAutoRotateEnabled => autoRotate;

    private void Awake()
    {
        ResolveCamera();
    }

    private void OnEnable()
    {
        dragging = false;
        activeTouchId = -1;
        pendingMouseDrag = false;
        yawVelocity = 0f;
    }

    public void SetInputCamera(Camera camera)
    {
        inputCamera = camera;
        mainCamera = camera;
    }

    public void SetRequirePointerOnCharacter(bool value)
    {
        requirePointerOnCharacter = value;
    }

    public void SetInvert(bool value)
    {
        invert = value;
    }

    public void SetAutoRotate(bool value)
    {
        autoRotate = value;
        if (autoRotate)
        {
            dragging = false;
            pendingMouseDrag = false;
            activeTouchId = -1;
            yawVelocity = 0f;
        }
    }

    public void SetAutoRotateSpeed(float degreesPerSecond)
    {
        autoRotateDegreesPerSecond = Mathf.Max(0f, degreesPerSecond);
    }

    private void Update()
    {
        ResolveCamera();

        bool hasTouch = MinimoInputBridge.TouchCount > 0;
        if (hasTouch)
        {
            HandleTouch();
        }
        else
        {
            HandleMouse();
        }

        if (autoRotate && Mathf.Abs(autoRotateDegreesPerSecond) > 0.001f)
        {
            float direction = invert ? 1f : -1f;
            transform.Rotate(Vector3.up, autoRotateDegreesPerSecond * direction * Time.deltaTime, Space.World);
        }
        else if (!dragging && useInertia && Mathf.Abs(yawVelocity) > 0.001f)
        {
            transform.Rotate(Vector3.up, yawVelocity * Time.deltaTime, Space.World);
            yawVelocity = Mathf.Lerp(yawVelocity, 0f, Mathf.Clamp01(inertiaDamping * Time.deltaTime));
        }
    }

    private void HandleMouse()
    {
        if (MinimoInputBridge.GetMouseButtonDown(0))
        {
            if (IsPointerBlockedByUI(-1))
            {
                dragging = false;
                pendingMouseDrag = false;
                return;
            }

            Vector2 pointer = MinimoInputBridge.MousePosition;
            if (!requirePointerOnCharacter || IsPointerOnCharacter(pointer))
            {
                dragging = false;
                pendingMouseDrag = true;
                lastPointerPosition = pointer;
                pointerDownPosition = lastPointerPosition;
            }
        }

        if (MinimoInputBridge.GetMouseButton(0))
        {
            Vector2 pointer = MinimoInputBridge.MousePosition;
            if (!dragging && pendingMouseDrag)
            {
                if (Vector2.Distance(pointer, pointerDownPosition) >= Mathf.Max(0f, dragActivationPixels))
                {
                    dragging = true;
                    pendingMouseDrag = false;
                    lastPointerPosition = pointer;
                }
            }

            if (dragging)
            {
                Vector2 delta = pointer - lastPointerPosition;
                lastPointerPosition = pointer;
                ApplyDelta(delta.x, Time.deltaTime);
            }
        }

        if (MinimoInputBridge.GetMouseButtonUp(0))
        {
            dragging = false;
            pendingMouseDrag = false;
        }
    }

    private void HandleTouch()
    {
        if (activeTouchId >= 0)
        {
            for (int i = 0; i < MinimoInputBridge.TouchCount; i++)
            {
                if (!MinimoInputBridge.TryGetTouch(i, out MinimoTouch touch))
                {
                    continue;
                }

                if (touch.fingerId != activeTouchId)
                {
                    continue;
                }

                if (touch.phase == UnityEngine.TouchPhase.Moved)
                {
                    ApplyDelta(touch.deltaPosition.x, Time.deltaTime);
                    lastPointerPosition = touch.position;
                }

                if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
                {
                    dragging = false;
                    activeTouchId = -1;
                }

                return;
            }

            dragging = false;
            activeTouchId = -1;
            return;
        }

        for (int i = 0; i < MinimoInputBridge.TouchCount; i++)
        {
            if (!MinimoInputBridge.TryGetTouch(i, out MinimoTouch touch))
            {
                continue;
            }

            if (touch.phase != UnityEngine.TouchPhase.Began)
            {
                continue;
            }

            if (IsPointerBlockedByUI(touch.fingerId))
            {
                continue;
            }

            if (requirePointerOnCharacter && !IsPointerOnCharacter(touch.position))
            {
                continue;
            }

            dragging = true;
            activeTouchId = touch.fingerId;
            lastPointerPosition = touch.position;
            return;
        }
    }

    private void ApplyDelta(float deltaX, float deltaTime)
    {
        float direction = invert ? -1f : 1f;
        float yaw = deltaX * rotationSpeed * direction;
        transform.Rotate(Vector3.up, yaw, Space.World);

        if (deltaTime > 0.0001f)
        {
            yawVelocity = yaw / deltaTime;
        }
    }

    private bool IsPointerBlockedByUI(int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (pointerId >= 0)
        {
            return EventSystem.current.IsPointerOverGameObject(pointerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsPointerOnCharacter(Vector2 screenPosition)
    {
        if (mainCamera == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Collide))
        {
            return false;
        }

        Transform hitTransform = hit.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    private void ResolveCamera()
    {
        if (inputCamera != null)
        {
            mainCamera = inputCamera;
            return;
        }

        if (mainCamera != null)
        {
            return;
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = MinimoUnityCompatibility.FindFirstObjectByType<Camera>();
        }
    }
}


internal static class MinimoInputBridge
{
    private const float InputSystemMouseDeltaScale = 0.1f;
    private const float InputSystemScrollDeltaScale = 1f / 120f;

    public static bool GetMouseButtonDown(int button)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(button);
#elif ENABLE_INPUT_SYSTEM
        ButtonControl control = GetMouseButtonControl(button);
        if (control != null && control.wasPressedThisFrame)
        {
            return true;
        }
        return button == 0 && Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true;
#else
        return false;
#endif
    }

    public static bool GetMouseButton(int button)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(button);
#elif ENABLE_INPUT_SYSTEM
        ButtonControl control = GetMouseButtonControl(button);
        if (control != null && control.isPressed)
        {
            return true;
        }
        return button == 0 && Touchscreen.current?.primaryTouch.press.isPressed == true;
#else
        return false;
#endif
    }

    public static bool GetMouseButtonUp(int button)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonUp(button);
#elif ENABLE_INPUT_SYSTEM
        ButtonControl control = GetMouseButtonControl(button);
        if (control != null && control.wasReleasedThisFrame)
        {
            return true;
        }
        return button == 0 && Touchscreen.current?.primaryTouch.press.wasReleasedThisFrame == true;
#else
        return false;
#endif
    }

    public static Vector2 MousePosition
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
            if (Touchscreen.current != null)
            {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }
            return Vector2.zero;
#else
            return Vector2.zero;
#endif
        }
    }

    public static Vector2 MouseScrollDelta
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta;
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue() * InputSystemScrollDeltaScale : Vector2.zero;
#else
            return Vector2.zero;
#endif
        }
    }

    public static int TouchCount
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.touchCount;
#elif ENABLE_INPUT_SYSTEM
            return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed ? 1 : 0;
#else
            return 0;
#endif
        }
    }

    public static bool TryGetTouch(int index, out MinimoTouch touch)
    {
        touch = default;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (index < 0 || index >= Input.touchCount)
        {
            return false;
        }
        Touch unityTouch = Input.GetTouch(index);
        touch = new MinimoTouch(unityTouch.fingerId, unityTouch.position, unityTouch.deltaPosition, unityTouch.phase);
        return true;
#elif ENABLE_INPUT_SYSTEM
        if (index != 0 || Touchscreen.current == null)
        {
            return false;
        }
        TouchControl primary = Touchscreen.current.primaryTouch;
        bool pressed = primary.press.isPressed;
        bool began = primary.press.wasPressedThisFrame;
        bool ended = primary.press.wasReleasedThisFrame;
        if (!pressed && !began && !ended)
        {
            return false;
        }
        UnityEngine.TouchPhase phase = began ? UnityEngine.TouchPhase.Began : ended ? UnityEngine.TouchPhase.Ended : UnityEngine.TouchPhase.Moved;
        touch = new MinimoTouch(primary.touchId.ReadValue(), primary.position.ReadValue(), primary.delta.ReadValue(), phase);
        return true;
#else
        return false;
#endif
    }

    public static bool GetKey(KeyCode key)
    {
        if (key == KeyCode.None)
        {
            return false;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(key);
#elif ENABLE_INPUT_SYSTEM
        ButtonControl control = GetButtonControl(key);
        return control != null && control.isPressed;
#else
        return false;
#endif
    }

    public static bool GetKeyDown(KeyCode key)
    {
        if (key == KeyCode.None)
        {
            return false;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
        ButtonControl control = GetButtonControl(key);
        return control != null && control.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool GetKeyUp(KeyCode key)
    {
        if (key == KeyCode.None)
        {
            return false;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyUp(key);
#elif ENABLE_INPUT_SYSTEM
        ButtonControl control = GetButtonControl(key);
        return control != null && control.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    public static float GetAxis(string axisName)
    {
        return ReadAxis(axisName, raw: false);
    }

    public static float GetAxisRaw(string axisName)
    {
        return ReadAxis(axisName, raw: true);
    }

    public static bool GetButtonDown(string buttonName)
    {
        return ReadNamedButton(buttonName, ButtonReadMode.Down);
    }

    public static bool GetButton(string buttonName)
    {
        return ReadNamedButton(buttonName, ButtonReadMode.Held);
    }

    public static bool GetButtonUp(string buttonName)
    {
        return ReadNamedButton(buttonName, ButtonReadMode.Up);
    }

    public static string[] GetJoystickNames()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetJoystickNames();
#elif ENABLE_INPUT_SYSTEM
        if (Gamepad.all.Count == 0)
        {
            return Array.Empty<string>();
        }
        string[] names = new string[Gamepad.all.Count];
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad pad = Gamepad.all[i];
            names[i] = pad != null ? pad.displayName : string.Empty;
        }
        return names;
#else
        return Array.Empty<string>();
#endif
    }

    private static float ReadAxis(string axisName, bool raw)
    {
        if (string.IsNullOrWhiteSpace(axisName))
        {
            return 0f;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            return raw ? Input.GetAxisRaw(axisName) : Input.GetAxis(axisName);
        }
        catch (ArgumentException)
        {
        }
#endif
#if ENABLE_INPUT_SYSTEM
        string normalized = NormalizeInputName(axisName);
        float keyboardHorizontal = 0f;
        float keyboardVertical = 0f;
        if (normalized == "horizontal" || normalized == "leftstickx" || normalized == "dpadx")
        {
            keyboardHorizontal = ReadKeyboardHorizontal();
        }
        if (normalized == "vertical" || normalized == "leftsticky" || normalized == "dpady")
        {
            keyboardVertical = ReadKeyboardVertical();
        }
        float value = normalized switch
        {
            "mousex" => Mouse.current != null ? Mouse.current.delta.x.ReadValue() * InputSystemMouseDeltaScale : 0f,
            "mousey" => Mouse.current != null ? Mouse.current.delta.y.ReadValue() * InputSystemMouseDeltaScale : 0f,
            "leftstickx" => Gamepad.current != null ? Gamepad.current.leftStick.x.ReadValue() : keyboardHorizontal,
            "leftsticky" => Gamepad.current != null ? Gamepad.current.leftStick.y.ReadValue() : keyboardVertical,
            "rightstickx" => Gamepad.current != null ? Gamepad.current.rightStick.x.ReadValue() : 0f,
            "rightsticky" => Gamepad.current != null ? Gamepad.current.rightStick.y.ReadValue() : 0f,
            "dpadx" => Gamepad.current != null ? Gamepad.current.dpad.x.ReadValue() : keyboardHorizontal,
            "dpady" => Gamepad.current != null ? Gamepad.current.dpad.y.ReadValue() : keyboardVertical,
            "horizontal" => Mathf.Abs(keyboardHorizontal) > 0.001f ? keyboardHorizontal : Gamepad.current != null ? Gamepad.current.leftStick.x.ReadValue() : 0f,
            "vertical" => Mathf.Abs(keyboardVertical) > 0.001f ? keyboardVertical : Gamepad.current != null ? Gamepad.current.leftStick.y.ReadValue() : 0f,
            _ => 0f
        };
        return Mathf.Clamp(value, -1f, 1f);
#else
        return 0f;
#endif
    }

    private static bool ReadNamedButton(string buttonName, ButtonReadMode mode)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
        {
            return false;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            return mode switch
            {
                ButtonReadMode.Down => Input.GetButtonDown(buttonName),
                ButtonReadMode.Held => Input.GetButton(buttonName),
                ButtonReadMode.Up => Input.GetButtonUp(buttonName),
                _ => false
            };
        }
        catch (ArgumentException)
        {
        }
#endif
#if ENABLE_INPUT_SYSTEM
        KeyCode key = NormalizeInputName(buttonName) switch
        {
            "jump" => KeyCode.Space,
            "submit" => KeyCode.Return,
            "cancel" => KeyCode.Escape,
            "fire1" => KeyCode.Mouse0,
            "fire2" => KeyCode.Mouse1,
            "fire3" => KeyCode.Mouse2,
            _ => KeyCode.None
        };
        return mode switch
        {
            ButtonReadMode.Down => GetKeyDown(key),
            ButtonReadMode.Held => GetKey(key),
            ButtonReadMode.Up => GetKeyUp(key),
            _ => false
        };
#else
        return false;
#endif
    }

    private static string NormalizeInputName(string value)
    {
        return value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

#if ENABLE_INPUT_SYSTEM
    private static ButtonControl GetMouseButtonControl(int button)
    {
        if (Mouse.current == null)
        {
            return null;
        }
        return button switch
        {
            0 => Mouse.current.leftButton,
            1 => Mouse.current.rightButton,
            2 => Mouse.current.middleButton,
            _ => null
        };
    }

    private static ButtonControl GetButtonControl(KeyCode key)
    {
        ButtonControl keyboardControl = GetKeyboardControl(key);
        if (keyboardControl != null)
        {
            return keyboardControl;
        }
        ButtonControl mouseControl = key switch
        {
            KeyCode.Mouse0 => GetMouseButtonControl(0),
            KeyCode.Mouse1 => GetMouseButtonControl(1),
            KeyCode.Mouse2 => GetMouseButtonControl(2),
            _ => null
        };
        return mouseControl ?? GetGamepadControl(key);
    }

    private static ButtonControl GetKeyboardControl(KeyCode key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return null;
        }
        return key switch
        {
            KeyCode.A => keyboard.aKey,
            KeyCode.B => keyboard.bKey,
            KeyCode.C => keyboard.cKey,
            KeyCode.D => keyboard.dKey,
            KeyCode.E => keyboard.eKey,
            KeyCode.Q => keyboard.qKey,
            KeyCode.S => keyboard.sKey,
            KeyCode.T => keyboard.tKey,
            KeyCode.W => keyboard.wKey,
            KeyCode.LeftArrow => keyboard.leftArrowKey,
            KeyCode.RightArrow => keyboard.rightArrowKey,
            KeyCode.UpArrow => keyboard.upArrowKey,
            KeyCode.DownArrow => keyboard.downArrowKey,
            KeyCode.Space => keyboard.spaceKey,
            KeyCode.Escape => keyboard.escapeKey,
            KeyCode.Return => keyboard.enterKey,
            KeyCode.LeftShift => keyboard.leftShiftKey,
            KeyCode.RightShift => keyboard.rightShiftKey,
            KeyCode.LeftControl => keyboard.leftCtrlKey,
            KeyCode.RightControl => keyboard.rightCtrlKey,
            KeyCode.LeftAlt => keyboard.leftAltKey,
            KeyCode.RightAlt => keyboard.rightAltKey,
            KeyCode.F1 => keyboard.f1Key,
            KeyCode.F2 => keyboard.f2Key,
            _ => null
        };
    }

    private static ButtonControl GetGamepadControl(KeyCode key)
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            return null;
        }
        return key switch
        {
            KeyCode.JoystickButton0 => gamepad.buttonSouth,
            KeyCode.JoystickButton1 => gamepad.buttonEast,
            KeyCode.JoystickButton2 => gamepad.buttonWest,
            KeyCode.JoystickButton3 => gamepad.buttonNorth,
            KeyCode.JoystickButton4 => gamepad.leftShoulder,
            KeyCode.JoystickButton5 => gamepad.rightShoulder,
            KeyCode.JoystickButton6 => gamepad.selectButton,
            KeyCode.JoystickButton7 => gamepad.startButton,
            KeyCode.JoystickButton8 => gamepad.leftStickButton,
            KeyCode.JoystickButton9 => gamepad.rightStickButton,
            _ => null
        };
    }

    private static float ReadKeyboardHorizontal()
    {
        float value = 0f;
        if (GetKey(KeyCode.D) || GetKey(KeyCode.RightArrow))
        {
            value += 1f;
        }
        if (GetKey(KeyCode.A) || GetKey(KeyCode.LeftArrow))
        {
            value -= 1f;
        }
        return value;
    }

    private static float ReadKeyboardVertical()
    {
        float value = 0f;
        if (GetKey(KeyCode.W) || GetKey(KeyCode.UpArrow))
        {
            value += 1f;
        }
        if (GetKey(KeyCode.S) || GetKey(KeyCode.DownArrow))
        {
            value -= 1f;
        }
        return value;
    }
#endif

    private enum ButtonReadMode
    {
        Down,
        Held,
        Up
    }
}

internal readonly struct MinimoTouch
{
    public readonly int fingerId;
    public readonly Vector2 position;
    public readonly Vector2 deltaPosition;
    public readonly UnityEngine.TouchPhase phase;

    public MinimoTouch(int fingerId, Vector2 position, Vector2 deltaPosition, UnityEngine.TouchPhase phase)
    {
        this.fingerId = fingerId;
        this.position = position;
        this.deltaPosition = deltaPosition;
        this.phase = phase;
    }
}


