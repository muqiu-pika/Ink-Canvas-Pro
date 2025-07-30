using System.ComponentModel;
using Newtonsoft.Json;

namespace Ink_Canvas.Resources
{
    /// <summary>
    /// 性能优化设置
    /// </summary>
    public class PerformanceSettings : INotifyPropertyChanged
    {
        [JsonProperty("enableThrottling")]
        public bool EnableThrottling { get; set; } = true;

        [JsonProperty("throttleIntervalMs")]
        public int ThrottleIntervalMs { get; set; } = 16; // 约60fps

        [JsonProperty("enableObjectPooling")]
        public bool EnableObjectPooling { get; set; } = true;

        [JsonProperty("enableBatchProcessing")]
        public bool EnableBatchProcessing { get; set; } = true;

        [JsonProperty("enableAsyncProcessing")]
        public bool EnableAsyncProcessing { get; set; } = true;

        [JsonProperty("enableRenderingCache")]
        public bool EnableRenderingCache { get; set; } = true;

        [JsonProperty("maxCacheSize")]
        public int MaxCacheSize { get; set; } = 1000;

        [JsonProperty("enableRegionRedraw")]
        public bool EnableRegionRedraw { get; set; } = true;

        [JsonProperty("enablePerformanceMonitoring")]
        public bool EnablePerformanceMonitoring { get; set; } = true;

        [JsonProperty("enableSmartSelection")]
        public bool EnableSmartSelection { get; set; } = true;

        [JsonProperty("enableLazyCircleUpdate")]
        public bool EnableLazyCircleUpdate { get; set; } = true;

        [JsonProperty("enableOptimizedTransform")]
        public bool EnableOptimizedTransform { get; set; } = true;

        [JsonProperty("maxStrokeTransformBatchSize")]
        public int MaxStrokeTransformBatchSize { get; set; } = 100;

        [JsonProperty("enableBackgroundProcessing")]
        public bool EnableBackgroundProcessing { get; set; } = true;

        [JsonProperty("enableMemoryOptimization")]
        public bool EnableMemoryOptimization { get; set; } = true;

        [JsonProperty("enableGarbageCollectionOptimization")]
        public bool EnableGarbageCollectionOptimization { get; set; } = true;

        [JsonProperty("performanceProfile")]
        public PerformanceProfile PerformanceProfile { get; set; } = PerformanceProfile.Balanced;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 应用性能配置文件
        /// </summary>
        public void ApplyPerformanceProfile(PerformanceProfile profile)
        {
            PerformanceProfile = profile;
            
            switch (profile)
            {
                case PerformanceProfile.HighPerformance:
                    EnableThrottling = true;
                    ThrottleIntervalMs = 8; // 120fps
                    EnableObjectPooling = true;
                    EnableBatchProcessing = true;
                    EnableAsyncProcessing = true;
                    EnableRenderingCache = true;
                    MaxCacheSize = 2000;
                    EnableRegionRedraw = true;
                    EnablePerformanceMonitoring = true;
                    EnableSmartSelection = true;
                    EnableLazyCircleUpdate = true;
                    EnableOptimizedTransform = true;
                    MaxStrokeTransformBatchSize = 200;
                    EnableBackgroundProcessing = true;
                    EnableMemoryOptimization = true;
                    EnableGarbageCollectionOptimization = true;
                    break;

                case PerformanceProfile.Balanced:
                    EnableThrottling = true;
                    ThrottleIntervalMs = 16; // 60fps
                    EnableObjectPooling = true;
                    EnableBatchProcessing = true;
                    EnableAsyncProcessing = true;
                    EnableRenderingCache = true;
                    MaxCacheSize = 1000;
                    EnableRegionRedraw = true;
                    EnablePerformanceMonitoring = false;
                    EnableSmartSelection = true;
                    EnableLazyCircleUpdate = true;
                    EnableOptimizedTransform = true;
                    MaxStrokeTransformBatchSize = 100;
                    EnableBackgroundProcessing = true;
                    EnableMemoryOptimization = true;
                    EnableGarbageCollectionOptimization = false;
                    break;

                case PerformanceProfile.PowerSaving:
                    EnableThrottling = true;
                    ThrottleIntervalMs = 33; // 30fps
                    EnableObjectPooling = false;
                    EnableBatchProcessing = true;
                    EnableAsyncProcessing = false;
                    EnableRenderingCache = false;
                    MaxCacheSize = 500;
                    EnableRegionRedraw = false;
                    EnablePerformanceMonitoring = false;
                    EnableSmartSelection = false;
                    EnableLazyCircleUpdate = false;
                    EnableOptimizedTransform = true;
                    MaxStrokeTransformBatchSize = 50;
                    EnableBackgroundProcessing = false;
                    EnableMemoryOptimization = true;
                    EnableGarbageCollectionOptimization = true;
                    break;

                case PerformanceProfile.Custom:
                    // 保持当前设置
                    break;
            }

            OnPropertyChanged(nameof(PerformanceProfile));
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public void ResetToDefaults()
        {
            ApplyPerformanceProfile(PerformanceProfile.Balanced);
        }
    }

    /// <summary>
    /// 性能配置文件
    /// </summary>
    public enum PerformanceProfile
    {
        /// <summary>
        /// 高性能模式 - 优先考虑性能
        /// </summary>
        HighPerformance,

        /// <summary>
        /// 平衡模式 - 性能和功耗平衡
        /// </summary>
        Balanced,

        /// <summary>
        /// 省电模式 - 优先考虑功耗
        /// </summary>
        PowerSaving,

        /// <summary>
        /// 自定义模式 - 用户自定义设置
        /// </summary>
        Custom
    }
} 