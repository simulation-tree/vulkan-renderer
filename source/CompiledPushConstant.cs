using Unmanaged;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPushConstant
    {
        public readonly RuntimeType componentType;
        public readonly RenderStage stage;

        public CompiledPushConstant(RuntimeType componentType, RenderStage stage)
        {
            this.componentType = componentType;
            this.stage = stage;
        }
    }
}