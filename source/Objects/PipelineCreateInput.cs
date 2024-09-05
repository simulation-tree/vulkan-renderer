using System;
using Unmanaged;

namespace Vulkan
{
    public readonly ref struct PipelineCreateInput
    {
        public readonly uint viewportCount;
        public readonly uint scissorCount;
        public readonly RenderPass renderPass;
        public readonly uint vertexBinding;
        public readonly ShaderModule vertex;
        public readonly ShaderModule fragment;
        public readonly USpan<VertexInputAttribute> vertexAttributes;

        public readonly LogicalDevice LogicalDevice => renderPass.logicalDevice;

        [Obsolete("Default constructor not supported", true)]
        public PipelineCreateInput()
        {
            throw new NotImplementedException();
        }

        public PipelineCreateInput(RenderPass renderPass, ShaderModule vertex, ShaderModule fragment, USpan<VertexInputAttribute> vertexAttributes)
        {
            this.renderPass = renderPass;
            this.vertex = vertex;
            this.fragment = fragment;
            this.vertexAttributes = vertexAttributes;
        }
    }
}
