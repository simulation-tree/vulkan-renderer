using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    [SkipLocalsInit]
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

        public Pipeline(PipelineCreateInput input, PipelineLayout layout, ReadOnlySpan<VkVertexInputBindingDescription> vertexBindings, ReadOnlySpan<VkVertexInputAttributeDescription> vertexAttributes, string entryPoint)
            : this(input, layout, vertexBindings, vertexAttributes, entryPoint.AsSpan())
        {
        }

        public Pipeline(PipelineCreateInput input, PipelineLayout layout, ReadOnlySpan<VkVertexInputBindingDescription> vertexBindings, ReadOnlySpan<VkVertexInputAttributeDescription> vertexAttributes, ReadOnlySpan<char> entryPoint)
        {
            logicalDevice = input.LogicalDevice;
            Span<byte> nameBuffer = stackalloc byte[entryPoint.Length + 1];
            for (int i = 0; i < entryPoint.Length; i++)
            {
                nameBuffer[i] = (byte)entryPoint[i];
            }

            nameBuffer[entryPoint.Length] = 0;
            Span<VkPipelineShaderStageCreateInfo> shaderStages = stackalloc VkPipelineShaderStageCreateInfo[2];
            shaderStages[0] = new()
            {
                stage = VkShaderStageFlags.Vertex,
                module = input.vertexShader.Value,
                pName = nameBuffer.GetPointer()
            };

            shaderStages[1] = new()
            {
                stage = VkShaderStageFlags.Fragment,
                module = input.fragmentShader.Value,
                pName = nameBuffer.GetPointer()
            };

            VkPipelineVertexInputStateCreateInfo vertexInputState = new()
            {
                vertexBindingDescriptionCount = (uint)vertexBindings.Length,
                pVertexBindingDescriptions = vertexBindings.GetPointer(),
                vertexAttributeDescriptionCount = (uint)vertexAttributes.Length,
                pVertexAttributeDescriptions = vertexAttributes.GetPointer()
            };

            VkPipelineInputAssemblyStateCreateInfo inputAssemblyState = new(VkPrimitiveTopology.TriangleList);
            VkPipelineViewportStateCreateInfo viewportState = new(1, 1);
            VkPipelineRasterizationStateCreateInfo rasterizationState = VkPipelineRasterizationStateCreateInfo.CullClockwise;
            VkPipelineMultisampleStateCreateInfo multisampleState = VkPipelineMultisampleStateCreateInfo.Default;

            VkPipelineDepthStencilStateCreateInfo depthStencilState = default;
            depthStencilState.sType = VkStructureType.PipelineDepthStencilStateCreateInfo;
            depthStencilState.depthTestEnable = input.depthTestEnable;
            depthStencilState.depthWriteEnable = input.depthWriteEnable;
            depthStencilState.depthCompareOp = (VkCompareOp)input.depthCompareOperation;
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

            System.Span<VkDynamicState> dynamicStateEnables = stackalloc VkDynamicState[2];
            dynamicStateEnables[0] = VkDynamicState.Viewport;
            dynamicStateEnables[1] = VkDynamicState.Scissor;

            VkPipelineDynamicStateCreateInfo dynamicState = new()
            {
                dynamicStateCount = 2,
                pDynamicStates = dynamicStateEnables.GetPointer()
            };

            VkGraphicsPipelineCreateInfo pipelineCreateInfo = new()
            {
                stageCount = 2,
                pStages = shaderStages.GetPointer(),
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

            VkResult result = vkCreateGraphicsPipeline(logicalDevice.Value, pipelineCreateInfo, out value);
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

            vkDestroyPipeline(logicalDevice.Value, value);
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