using Shaders;
using System;
using Vortice.Vulkan;

namespace Rendering.Vulkan
{
    public static class ShaderTypeExtensions
    {
        public static VkShaderStageFlags GetShaderStage(this ShaderType stage)
        {
            return stage switch
            {
                ShaderType.Vertex => VkShaderStageFlags.Vertex,
                ShaderType.Fragment => VkShaderStageFlags.Fragment,
                ShaderType.Geometry => VkShaderStageFlags.Geometry,
                ShaderType.Compute => VkShaderStageFlags.Compute,
                _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
            };
        }
    }
}