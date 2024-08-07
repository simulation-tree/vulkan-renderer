using System;

namespace Vulkan
{
    public readonly ref struct PipelineCreateInput
    {
        public readonly uint viewportCount;
        public readonly uint scissorCount;
        public readonly ReadOnlySpan<DescriptorSetLayout> setLayouts;
        public readonly RenderPass renderPass;
        public readonly uint vertexBinding;
        public readonly ShaderModule vertex;
        public readonly ShaderModule fragment;
        public readonly ReadOnlySpan<VertexInputAttribute> vertexAttributes;

        public readonly LogicalDevice LogicalDevice => renderPass.logicalDevice;

        [Obsolete("Default constructor not supported", true)]
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
