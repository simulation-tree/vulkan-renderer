using System;

namespace Vulkan
{
    public ref struct PipelineCreateInput
    {
        public uint viewportCount;
        public uint scissorCount;
        public ReadOnlySpan<DescriptorSetLayout> setLayouts;
        public RenderPass renderPass;
        public uint vertexBinding;
        public ShaderModule vertex;
        public ShaderModule fragment;
        public ReadOnlySpan<VertexInputAttribute> vertexAttributes;

        public readonly LogicalDevice LogicalDevice => renderPass.logicalDevice;

        public PipelineCreateInput()
        {
            throw new NotImplementedException();
        }

        public PipelineCreateInput(ReadOnlySpan<DescriptorSetLayout> setLayouts, RenderPass renderPass, ShaderModule vertex, ShaderModule fragment, ReadOnlySpan<VertexInputAttribute> vertexAttributes)
        {
            this.setLayouts = setLayouts;
            this.renderPass = renderPass;
            this.vertex = vertex;
            this.fragment = fragment;
            this.vertexAttributes = vertexAttributes;
        }
    }
}
