using UnityEngine;

using UnityEngine.Video;

namespace SignatureLogo
{
    /// 视频背景：把指定的 VideoClip 渲染到 Logo 平面后方一块"铺满镜头"的世界空间 Quad 上（循环、无声）。
    /// - 运行时自动创建 Quad + VideoPlayer（URP/Unlit 材质 + RenderTexture），无需手工摆位
    /// - Quad 位于比 Logo 平面更远离相机的一侧，深度排序天然保证拼贴 Logo / 飞行签名全部显示在视频前面
    /// - 相机 FOV / 屏幕宽高比变化时自动重算铺满尺寸（Cover：等比放大、溢出裁边）
    [DisallowMultipleComponent]
    [AddComponentMenu("SignatureLogo/Video Background (视频背景)")]
    public sealed class VideoBackground : MonoBehaviour
    {
        [Header("Video")]
        [SerializeField, Tooltip("作为背景播放的视频（自动循环、无声）")]
        VideoClip videoClip;

        [SerializeField, Tooltip("是否循环播放")]
        bool loop = true;

        [SerializeField, Range(0f, 1f), Tooltip("背景亮度（乘色）；调低可让前面的签名 Logo 更醒目")]
        float brightness = 1f;

        [Header("Placement")]
        [SerializeField, Min(0.1f), Tooltip("背景 Quad 比本物体所在平面（= Logo 平面）再远离相机多少世界单位")]
        float behindLogoDistance = 5f;

        const float FitMargin = 1.002f; // 铺满留一点余量，避免屏幕边缘露缝

        Transform _quad;
        VideoPlayer _player;
        RenderTexture _rt;
        Camera _cam;
        float _cachedAspect = -1f;
        float _cachedFov = -1f;
        int _cachedScreenW = -1;
        int _cachedScreenH = -1;

        void Awake()
        {
            if (videoClip == null)
            {
                Debug.LogWarning("[VideoBackground] 未指定 VideoClip，本组件停用。", this);
                enabled = false;
                return;
            }
            if (!BuildQuad())
            {
                enabled = false;
                return;
            }
            BuildPlayer();

            // 立即铺满一次，避免第一帧出现原始 1x1 的 Quad
            _cam = Camera.main;
            if (_cam != null) Refit();
        }

        void OnEnable()
        {
            if (_player != null) _player.Play();
        }

        void OnDisable()
        {
            if (_player != null) _player.Pause();
        }

        void OnDestroy()
        {
            if (_player != null) Destroy(_player);
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
            if (_quad != null) Destroy(_quad.gameObject);
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || _quad == null) return;
            if (LayoutChanged()) Refit();
        }

        bool LayoutChanged()
        {
            return _cachedAspect != _cam.aspect
                   || _cachedFov != _cam.fieldOfView
                   || _cachedScreenW != Screen.width
                   || _cachedScreenH != Screen.height;
        }

        /// 把 Quad 摆到"Logo 平面再往后 behindLogoDistance"处、面向相机，并按该深度的视口尺寸等比铺满（Cover）。
        void Refit()
        {
            _cachedAspect = _cam.aspect;
            _cachedFov = _cam.fieldOfView;
            _cachedScreenW = Screen.width;
            _cachedScreenH = Screen.height;

            var camPos = _cam.transform.position;
            var forward = _cam.transform.forward;

            // 本物体所在平面（= Logo 平面，与 LogoRoot 同在原点平面）沿视线方向的距离
            float anchorDist = Vector3.Dot(transform.position - camPos, forward);
            if (anchorDist < 0.5f) anchorDist = 10f;
            float quadDist = anchorDist + behindLogoDistance;

            _quad.position = camPos + forward * quadDist;
            // Quad 网格的可见面在它的 -Z 侧：让 local +Z 顺着相机 forward 指向远方，
            // 正面（-Z 法线）才朝向相机。若把 +Z 转向相机会变成背面朝屏、被整体剔除。
            _quad.rotation = Quaternion.LookRotation(forward, _cam.transform.up);

            float viewH = 2f * quadDist * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float viewW = viewH * _cam.aspect;
            float videoAspect = VideoAspect();
            float sx, sy;
            if (viewW / viewH > videoAspect)
            {
                sx = viewW;              // 视口更宽：按宽度铺满，上下溢出
                sy = viewW / videoAspect;
            }
            else
            {
                sy = viewH;              // 视口更窄：按高度铺满，左右溢出
                sx = viewH * videoAspect;
            }
            _quad.localScale = new Vector3(sx * FitMargin, sy * FitMargin, 1f);
        }

        float VideoAspect()
        {
            if (videoClip != null && videoClip.width > 0 && videoClip.height > 0)
                return (float)videoClip.width / videoClip.height;
            return 16f / 9f;
        }

        /// 加载视频着色器：Resources 内置优先，Shader.Find 仅作兜底。
        /// 打包时未被任何资产引用的着色器会被剥离导致 Shader.Find 返回空，
        /// 所以 VideoUnlit 必须放在 Resources 目录（Resources 资源始终进包）。
        static Shader LoadVideoShader()
        {
            var shader = Resources.Load<Shader>("VideoUnlit");
            if (shader != null) return shader;
            shader = Shader.Find("SignatureLogo/VideoUnlit");
            if (shader != null) return shader;
            shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) return shader;
            return Shader.Find("Unlit/Texture"); // 兜底链尽头；Resources 正常时走不到
        }

        bool BuildQuad()
        {
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            Destroy(_quad.GetComponent<Collider>()); // 背景不参与物理与 UI 射线
            _quad.name = "VideoQuad";
            _quad.SetParent(transform, false);

            var shader = LoadVideoShader();
            if (shader == null)
            {
                Debug.LogError("[VideoBackground] 找不到可用的视频着色器，本组件停用。", this);
                return false;
            }

            var renderer = _quad.GetComponent<MeshRenderer>();
            var material = new Material(shader);

            int w = videoClip.width > 0 ? (int)videoClip.width : 1920;
            int h = videoClip.height > 0 ? (int)videoClip.height : 1080;
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "VideoBackgroundRT" };

            material.mainTexture = _rt;
            var dim = new Color(brightness, brightness, brightness, 1f);
            if (material.HasProperty("_Tint")) material.SetColor("_Tint", dim);           // SignatureLogo/VideoUnlit
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", dim); // URP/Unlit 兜底
            if (material.HasProperty("_Color")) material.SetColor("_Color", dim);         // 内置 Unlit/Texture 兜底
            renderer.sharedMaterial = material;
            return true;
        }

        void BuildPlayer()
        {
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _rt;
            _player.isLooping = loop;
            // 不设置任何 targetAudioSource（AudioSource 模式 + 空音源列表 = 无声），兼容所有 Unity 版本
            _player.clip = videoClip;
        }
    }
}
