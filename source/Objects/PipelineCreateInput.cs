using Materials;
using System;
using Unmanaged;

namespace Vulkan
{
    public ref struct PipelineCreateInput
    {
        public readonly RenderPass renderPass;
        public uint vertexBinding;
        public ShaderModule vertexShader;
        public ShaderModule fragmentShader;
        public USpan<VertexInputAttribute> vertexAttributes;
        public bool depthTestEnable;
        public bool depthWriteEnable;
        public CompareOperation depthCompareOperation;

        public readonly LogicalDevice LogicalDevice => renderPass.logicalDevice;

        [Obsolete("Default constructor not supported", true)]
        public PipelineCreateInput()
        {
            throw new NotImplementedException();
        }

        public PipelineCreateInput(RenderPass renderPass, ShaderModule vertexShader, ShaderModule fragmentShader, USpan<VertexInputAttribute> vertexAttributes)
        {
            this.renderPass = renderPass;
            this.vertexShader = vertexShader;
            this.fragmentShader = fragmentShader;
            this.vertexAttributes = vertexAttributes;
            depthCompareOperation = CompareOperation.Less;
            depthWriteEnable = true;
            depthTestEnable = true;
        }
    }
}