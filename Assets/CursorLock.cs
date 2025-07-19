using UnityEngine;
using UnityEngine.UI;

public class CursorLock : MonoBehaviour
{
    private bool showInstructions = true;
    private string instructionText = "Press ESC to lock/unlock cursor";
    private GUIStyle guiStyle;
    [SerializeField] private PlayerMovement _player;
    private InputManager _input;
    private bool _isESCPressed;
    private bool _wasESCPressedLastFrame;

    void Start()
    {
        _input = ServiceLocator.Get<InputManager>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        guiStyle = new GUIStyle();
        guiStyle.fontSize = 20;
        guiStyle.normal.textColor = Color.white;
        guiStyle.alignment = TextAnchor.UpperCenter;
    }

    void Update()
    {
        bool escPressedThisFrame = _input.CursorLockPressed;
        _isESCPressed = escPressedThisFrame && !_wasESCPressedLastFrame;
        _wasESCPressedLastFrame = escPressedThisFrame;

        if (_isESCPressed) ToggleCursor();
    }

    void OnGUI()
    {
        if (showInstructions)
        {
            Rect labelRect = new Rect(0, 10, Screen.width, 30);
            GUI.Label(labelRect, instructionText, guiStyle);
        }
    }

    void ToggleCursor() {
        Debug.Log("ToggleCursor");
        if (Cursor.lockState == CursorLockMode.Locked) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        } else {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
