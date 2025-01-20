using Worlds;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPushConstant
    {
        public readonly DataType componentType;
        public readonly RenderStage stage;

        public CompiledPushConstant(DataType componentType, RenderStage stage)
        {
            this.componentType = componentType;
            this.stage = stage;
        }
    }
}