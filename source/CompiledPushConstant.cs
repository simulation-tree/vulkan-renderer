using Worlds;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPushConstant
    {
        public readonly ComponentType componentType;
        public readonly RenderStage stage;

        public CompiledPushConstant(ComponentType componentType, RenderStage stage)
        {
            this.componentType = componentType;
            this.stage = stage;
        }
    }
}