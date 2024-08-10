using Shaders;
using Unmanaged;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPushConstant
    {
        public readonly RuntimeType componentType;
        public readonly ShaderStage stage;

        public CompiledPushConstant(RuntimeType componentType, ShaderStage stage)
        {
            this.componentType = componentType;
            this.stage = stage;
        }
    }
}