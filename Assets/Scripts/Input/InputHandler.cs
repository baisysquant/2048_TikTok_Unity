using UnityEngine;

public class InputHandler : MonoBehaviour
{
    private GameManager _gameManager;
    private Vector2 _touchStartPos;
    private bool _isSwiping;
    private float _swipeThreshold = 30f;
    private float _lastMoveTime;
    private const float MoveCooldown = 0.1f;

    private void Awake()
    {
        _gameManager = GetComponent<GameManager>();
    }

    public void Initialize()
    {
    }

    private void Update()
    {
        if (_gameManager == null || _gameManager.Game == null)
            return;

        HandleKeyboard();
        HandleTouch();
        HandleMouse();
    }

    private bool CanMove()
    {
        return Time.time - _lastMoveTime >= MoveCooldown;
    }

    private void HandleKeyboard()
    {
        if (!CanMove()) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            _gameManager.OnMove(MoveDirection.Left);
            _lastMoveTime = Time.time;
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            _gameManager.OnMove(MoveDirection.Right);
            _lastMoveTime = Time.time;
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            _gameManager.OnMove(MoveDirection.Up);
            _lastMoveTime = Time.time;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            _gameManager.OnMove(MoveDirection.Down);
            _lastMoveTime = Time.time;
        }
    }

    private void HandleTouch()
    {
        if (Input.touchCount <= 0) return;

        var touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                TiktokAudioBridge.UnlockOnTouch();
                _touchStartPos = touch.position;
                _isSwiping = true;
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                if (_isSwiping)
                {
                    DetectSwipe(touch.position - _touchStartPos);
                }
                _isSwiping = false;
                break;
        }
    }

    private void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _touchStartPos = Input.mousePosition;
            _isSwiping = true;
        }

        if (Input.GetMouseButtonUp(0) && _isSwiping)
        {
            Vector2 delta = (Vector2)Input.mousePosition - _touchStartPos;
            DetectSwipe(delta);
            _isSwiping = false;
        }
    }

    private void DetectSwipe(Vector2 delta)
    {
        if (delta.magnitude < _swipeThreshold) return;
        if (!CanMove()) return;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        if (angle >= -45 && angle < 45)
            _gameManager.OnMove(MoveDirection.Right);
        else if (angle >= 45 && angle < 135)
            _gameManager.OnMove(MoveDirection.Up);
        else if (angle >= -135 && angle < -45)
            _gameManager.OnMove(MoveDirection.Down);
        else
            _gameManager.OnMove(MoveDirection.Left);

        _lastMoveTime = Time.time;
    }
}
