using System;

using System.Collections.Generic;

using UnityEngine;

namespace SignatureLogo
{
    /// 对外唯一接入契约。签名团队持本接口引用调用（GetComponent&lt;ISignatureLogoVisualizer&gt;()）。
    /// 注意：Start 与 MonoBehaviour 生命周期消息重名，实现类用显式接口实现规避。
    /// 所有成员仅支持主线程调用。
    public interface ISignatureLogoVisualizer
    {
        /// 当前全部签名 Sprite（只读视图，无法修改内部数据）。
        IReadOnlyList<Sprite> Sprites { get; }

        /// Logo 切换间隔（秒）。≤0 会被钳制为 1。默认 15。
        float LogoSwitchInterval { get; set; }

        VisualizerState State { get; }

        /// 加入一个签名（null 安全跳过）。运行中加入在下次 Formation 生效。
        void AddSprite(Sprite sprite);

        /// 批量加入签名。
        void AddSprites(IEnumerable<Sprite> sprites);

        /// 清空全部签名：立即停止显示并回 Idle，等下一次 Start()。
        void ClearSprites();

        /// 进入 Logo Formation 循环（从左到右拼接 → 等待 → 下一个 Logo → 无限循环）。
        void Start();

        /// 某个 Logo 开始 Formation（参数：Logo 序号）。
        event Action<int> LogoFormationStarted;

        /// 某个 Logo Formation 完成（参数：Logo 序号）。
        event Action<int> LogoFormationCompleted;
    }
}
