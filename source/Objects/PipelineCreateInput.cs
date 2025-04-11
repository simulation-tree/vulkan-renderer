using Materials;
using System;

namespace Vulkan
{
    public ref struct PipelineCreateInput
    {
        public readonly RenderPass renderPass;
        public uint vertexBinding;
        public ShaderModule vertexShader;
        public ShaderModule fragmentShader;
        public BlendSettings blendSettings;
        public DepthSettings depthSettings;

        public readonly LogicalDevice LogicalDevice => renderPass.logicalDevice;

        [Obsolete("Default constructor not supported", true)]
        public PipelineCreateInput()
        {
            throw new NotImplementedException();
        }

        public PipelineCreateInput(RenderPass renderPass, ShaderModule vertexShader, ShaderModule fragmentShader)
        {
            this.renderPass = renderPass;
            this.vertexShader = vertexShader;
            this.fragmentShader = fragmentShader;
            blendSettings = default;
            depthSettings = default;
        }
    }
}