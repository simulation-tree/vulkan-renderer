using System;
using System.Diagnostics;
using Unmanaged;
using Vortice.Vulkan;

namespace Vulkan
{
    public unsafe struct Pipeline : IDisposable, IEquatable<Pipeline>
    {
        private readonly VkPipeline value;
        private readonly LogicalDevice logicalDevice;
        private bool valid;

        public readonly VkPipeline Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public Pipeline(PipelineCreateInput input, PipelineLayout layout, string entryPoint)
            : this(input, layout, entryPoint.AsSpan())
        {
        }

        public Pipeline(PipelineCreateInput input, PipelineLayout layout, USpan<char> entryPoint)
        {
            logicalDevice = input.LogicalDevice;
            byte* nameBuffer = stackalloc byte[(int)entryPoint.Length];
            for (uint i = 0; i < entryPoint.Length; i++)
            {
                nameBuffer[i] = (byte)entryPoint[i];
            }

            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[2];
            shaderStages[0] = new()
            {
                stage = VkShaderStageFlags.Vertex,
                module = input.vertex.Value,
                pName = nameBuffer
            };

            shaderStages[1] = new()
            {
                stage = VkShaderStageFlags.Fragment,
                module = input.fragment.Value,
                pName = nameBuffer
            };

            VkVertexInputAttributeDescription* attributes = stackalloc VkVertexInputAttributeDescription[(int)input.vertexAttributes.Length];
            uint offset = 0;
            for (uint i = 0; i < input.vertexAttributes.Length; i++)
            {
                VertexInputAttribute attribute = input.vertexAttributes[i];
                attributes[i] = new(attribute.location, attribute.format, offset, attribute.binding);
                offset += attribute.size;
            }

            VkVertexInputBindingDescription vertexInputBinding = new(offset, VkVertexInputRate.Vertex, input.vertexBinding);
            VkPipelineVertexInputStateCreateInfo vertexInputState = new()
            {
                vertexBindingDescriptionCount = 1,
                pVertexBindingDescriptions = &vertexInputBinding,
                vertexAttributeDescriptionCount = input.vertexAttributes.Length,
                pVertexAttributeDescriptions = attributes
            };

            VkPipelineInputAssemblyStateCreateInfo inputAssemblyState = new(VkPrimitiveTopology.TriangleList);
            VkPipelineViewportStateCreateInfo viewportState = new(1, 1);
            VkPipelineRasterizationStateCreateInfo rasterizationState = VkPipelineRasterizationStateCreateInfo.CullClockwise;
            VkPipelineMultisampleStateCreateInfo multisampleState = VkPipelineMultisampleStateCreateInfo.Default;

            VkPipelineDepthStencilStateCreateInfo depthStencilState = default;
            depthStencilState.sType = VkStructureType.PipelineDepthStencilStateCreateInfo;
            depthStencilState.depthTestEnable = true;
            depthStencilState.depthWriteEnable = true;
            depthStencilState.depthCompareOp = VkCompareOp.Less;
            depthStencilState.depthBoundsTestEnable = false;
            depthStencilState.stencilTestEnable = false;
            depthStencilState.minDepthBounds = 0f;
            depthStencilState.maxDepthBounds = 1f;

            bool supportsAlpha = false;
            VkPipelineColorBlendAttachmentState blendAttachmentState = default;
            blendAttachmentState.blendEnable = supportsAlpha;
            if (supportsAlpha)
            {
                blendAttachmentState.srcColorBlendFactor = VkBlendFactor.SrcAlpha;
                blendAttachmentState.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                blendAttachmentState.colorBlendOp = VkBlendOp.Add;
                blendAttachmentState.srcAlphaBlendFactor = VkBlendFactor.One;
                blendAttachmentState.dstAlphaBlendFactor = VkBlendFactor.Zero;
                blendAttachmentState.alphaBlendOp = VkBlendOp.Add;
            }

            blendAttachmentState.colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A;

            VkPipelineColorBlendStateCreateInfo colorBlending = new(blendAttachmentState);
            colorBlending.logicOpEnable = false;
            colorBlending.logicOp = VkLogicOp.Copy;
            colorBlending.blendConstants[0] = 0f;
            colorBlending.blendConstants[1] = 0f;
            colorBlending.blendConstants[2] = 0f;
            colorBlending.blendConstants[3] = 0f;

            VkDynamicState* dynamicStateEnables = stackalloc VkDynamicState[2];
            dynamicStateEnables[0] = VkDynamicState.Viewport;
            dynamicStateEnables[1] = VkDynamicState.Scissor;

            VkPipelineDynamicStateCreateInfo dynamicState = new()
            {
                dynamicStateCount = 2,
                pDynamicStates = dynamicStateEnables
            };

            VkGraphicsPipelineCreateInfo pipelineCreateInfo = new()
            {
                stageCount = 2,
                pStages = shaderStages,
                pVertexInputState = &vertexInputState,
                pInputAssemblyState = &inputAssemblyState,
                pTessellationState = null,
                pViewportState = &viewportState,
                pRasterizationState = &rasterizationState,
                pMultisampleState = &multisampleState,
                pDepthStencilState = &depthStencilState,
                pColorBlendState = &colorBlending,
                pDynamicState = &dynamicState,
                layout = layout.Value,
                renderPass = input.renderPass.Value
            };

            VkResult result = Vortice.Vulkan.Vulkan.vkCreateGraphicsPipeline(logicalDevice.Value, pipelineCreateInfo, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create graphics pipeline: {result}");
            }

            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Pipeline));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            Vortice.Vulkan.Vulkan.vkDestroyPipeline(logicalDevice.Value, value);
            valid = false;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Pipeline pipeline && Equals(pipeline);
        }

        public readonly bool Equals(Pipeline other)
        {
            if (IsDisposed && other.IsDisposed)
            {
                return true;
            }

            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(Pipeline left, Pipeline right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Pipeline left, Pipeline right)
        {
            return !(left == right);
        }
    }
}
