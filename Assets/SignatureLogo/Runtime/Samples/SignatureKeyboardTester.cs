using UnityEngine;
using UnityEngine.InputSystem;

namespace SignatureLogo
{
    /// 测试辅助组件：按 1 = Start()（开始/恢复播放），按 2 = Stop()（冻结画面）。
    /// 挂在与 SignatureLogoVisualizer 同一物体上即可；每次按键都会在 Console 打印调用记录。
    [AddComponentMenu("SignatureLogo/Keyboard Tester (1=Start 2=Stop)")]
    public sealed class SignatureKeyboardTester : MonoBehaviour
    {
        [SerializeField, Tooltip("开始/恢复播放的按键")]
        Key startKey = Key.Digit1;

        [SerializeField, Tooltip("停止（冻结）播放的按键")]
        Key stopKey = Key.Digit2;

        ISignatureLogoVisualizer _viz;
        bool _warned;

        void Awake()
        {
            _viz = GetComponent<ISignatureLogoVisualizer>();
        }

        void Update()
        {
            if (_viz == null)
            {
                if (!_warned)
                {
                    Debug.LogWarning("[SignatureKeyboardTester] 同物体上找不到 ISignatureLogoVisualizer 组件。", this);
                    _warned = true;
                }
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[startKey].wasPressedThisFrame)
            {
                Debug.Log("[SignatureKeyboardTester] 按键 " + startKey + " → Start()（当前状态：" + vizState() + "）", this);
                _viz.Start();
            }

            if (keyboard[stopKey].wasPressedThisFrame)
            {
                Debug.Log("[SignatureKeyboardTester] 按键 " + stopKey + " → Stop()（当前状态：" + vizState() + "）", this);
                _viz.Stop();
            }
        }

        string vizState()
        {
            return _viz.State.ToString();
        }
    }
}
