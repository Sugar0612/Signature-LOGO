namespace SignatureLogo
{
    /// 可视化系统状态机（由门面驱动转移）。
    public enum VisualizerState
    {
        /// 未开始 / 已清空。
        Idle,
        /// 正在从左到右组装当前 Logo。
        Forming,
        /// Formation 完成，等待 LogoSwitchInterval 后切换下一个 Logo。
        Holding,
        /// 已停止：画面冻结在当前位置（签名保留），轮换计时停止；Start() 恢复播放。
        Stopped
    }
}
