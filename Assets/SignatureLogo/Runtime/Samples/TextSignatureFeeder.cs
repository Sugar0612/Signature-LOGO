using UnityEngine;

namespace SignatureLogo
{
    /// 演示组件：用字体把一串字符渲染成签名 Sprite（"张三李四王五…"）喂给系统。
    /// 已有签名图片时：把 PNG（Texture Type 设为 Sprite (2D and UI)）直接拖到 ManualSprites，会优先使用。
    /// 注意：与本组件同物体上的 DemoSignatureFeeder 二选一（本组件 Awake 时会自动禁用它）。
    [AddComponentMenu("SignatureLogo/Text Signature Feeder (字体签名)")]
    public sealed class TextSignatureFeeder : MonoBehaviour
    {
        [SerializeField, TextArea, Tooltip("要渲染成签名的字符（每个字符一个签名，自动去重）")]
        string characters = "张三李四王五赵六钱七孙八周九吴十";

        [SerializeField, Tooltip("每个字符一个签名；关闭则整段文字合成一个签名")]
        bool oneSpritePerCharacter = true;

        [SerializeField, Tooltip("自定义字体资产；留空 = 自动用系统中文字体（微软雅黑等）")]
        Font customFont;

        [SerializeField, Min(8), Tooltip("渲染字号；同时决定世界尺寸（PPU=字号，字高≈1 世界单位）")]
        int fontSize = 96;

        [SerializeField, Tooltip("每个字符随机颜色；关闭则全部白色")]
        bool colorful = true;

        [SerializeField, Tooltip("已有签名图片直接拖这里（非空时忽略上面的文字设置）")]
        Sprite[] manualSprites;

        [SerializeField, Min(1f)] float demoSwitchInterval = 10f;

        ISignatureLogoVisualizer _viz;

        void Awake()
        {
            _viz = GetComponent<ISignatureLogoVisualizer>();

            // 防呆：同物体上的 DemoSignatureFeeder 会往签名池混入随机乱线，导致画面出现"不是我的签名"
            var demo = GetComponent<DemoSignatureFeeder>();
            if (demo != null && demo.enabled)
            {
                demo.enabled = false;
                Debug.LogWarning("[TextSignatureFeeder] 已自动禁用同物体上的 DemoSignatureFeeder（两套签名来源会混在一起轮流使用）。", this);
            }
        }

        void Start()
        {
            if (_viz == null) return;

            Sprite[] sprites;
            if (manualSprites != null && manualSprites.Length > 0)
            {
                sprites = manualSprites;
                Debug.Log("[TextSignatureFeeder] 使用手动指定的 " + sprites.Length + " 张签名图片。");
            }
            else
            {
                var font = customFont != null ? customFont : TextSpriteFactory.CreateDefaultChineseFont(fontSize);
                sprites = TextSpriteFactory.CreateSprites(characters, font, fontSize, colorful, oneSpritePerCharacter);
                Debug.Log("[TextSignatureFeeder] 已用字体生成 " + sprites.Length + " 个签名 Sprite。");
            }

            if (sprites.Length == 0)
            {
                Debug.LogError("[TextSignatureFeeder] 没有生成任何签名 Sprite，请检查字符/字体设置。");
                return;
            }

            _viz.AddSprites(sprites);
            _viz.LogoSwitchInterval = demoSwitchInterval;
            _viz.Start();
        }
    }
}
