using Shaders;
using Worlds;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPushConstant
    {
        public readonly DataType componentType;
        public readonly ShaderType stage;

        public CompiledPushConstant(DataType componentType, ShaderType stage)
        {
            this.componentType = componentType;
            this.stage = stage;
        }
    }
}