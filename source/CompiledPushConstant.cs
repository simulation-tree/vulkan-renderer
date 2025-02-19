using Shaders;
using Vortice.Vulkan;
using Worlds;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPushConstant
    {
        public readonly DataType componentType;
        public readonly ShaderType shaderType;
        public readonly VkShaderStageFlags stageFlags;

        public CompiledPushConstant(DataType componentType, ShaderType shaderType, VkShaderStageFlags stageFlags)
        {
            this.componentType = componentType;
            this.shaderType = shaderType;
            this.stageFlags = stageFlags;
        }
    }
}