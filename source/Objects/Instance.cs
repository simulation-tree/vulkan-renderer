using Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Instance : IDisposable, IEquatable<Instance>
    {
        private static readonly ASCIIText256[] preferredValidationLayers =
        [
            "VK_LAYER_KHRONOS_validation"
        ];

        private static readonly ASCIIText256[] fallbackValidationLayers =
        [
            "VK_LAYER_LUNARG_standard_validation"
        ];

        private static readonly ASCIIText256[] fallbackIndividualLayers =
        [
            "VK_LAYER_GOOGLE_threading",
            "VK_LAYER_LUNARG_parameter_validation",
            "VK_LAYER_LUNARG_object_tracker",
            "VK_LAYER_LUNARG_core_validation",
            "VK_LAYER_GOOGLE_unique_objects",
        ];

        private static readonly ASCIIText256[] fallbackFallbackLayers =
        [
            "VK_LAYER_LUNARG_core_validation"
        ];

        public readonly VkDebugUtilsMessengerEXT debugMessenger;

        private readonly Array<PhysicalDevice> physicalDevices;
        private readonly VkInstance value;
        private readonly Text applicationName;
        private readonly Text engineName;
        private bool valid;

        public readonly VkInstance Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly nint Address => value.Handle;

        public readonly ReadOnlySpan<PhysicalDevice> PhysicalDevices
        {
            get
            {
                ThrowIfDisposed();

                return physicalDevices.AsSpan();
            }
        }

        public readonly bool IsDisposed => !valid;

        public readonly ReadOnlySpan<char> ApplicationName
        {
            get
            {
                ThrowIfDisposed();

                return applicationName.AsSpan();
            }
        }

        public readonly ReadOnlySpan<char> EngineName
        {
            get
            {
                ThrowIfDisposed();

                return engineName.AsSpan();
            }
        }

        internal Instance(Library library, ReadOnlySpan<char> applicationName, ReadOnlySpan<char> engineName, ReadOnlySpan<ASCIIText256> extensions)
        {
            using List<ASCIIText256> inputLayers = new();

#if DEBUG
            using Array<ASCIIText256> globalLayers = library.GetGlobalLayers();
            if (ContainsAll(globalLayers.AsSpan(), preferredValidationLayers))
            {
                inputLayers.AddRange(preferredValidationLayers);
            }
            else if (ContainsAll(globalLayers.AsSpan(), fallbackValidationLayers))
            {
                inputLayers.AddRange(fallbackValidationLayers);
            }
            else if (ContainsAll(globalLayers.AsSpan(), fallbackIndividualLayers))
            {
                inputLayers.AddRange(fallbackIndividualLayers);
            }
            else if (ContainsAll(globalLayers.AsSpan(), fallbackFallbackLayers))
            {
                inputLayers.AddRange(fallbackFallbackLayers);
            }
            else
            {
                if (globalLayers.Length > 0)
                {
                    using Text remaining = new();
                    Span<char> buffer = stackalloc char[ASCIIText256.Capacity];
                    foreach (ASCIIText256 layer in globalLayers)
                    {
                        int length = layer.CopyTo(buffer);
                        remaining.Append(buffer.Slice(0, length));
                        remaining.Append(',');
                        remaining.Append(' ');
                    }

                    remaining.SetLength(remaining.Length - 2);
                    Trace.WriteLine($"No suitable validation layers found, there were instead:\n{remaining}");
                }
                else
                {
                    Trace.WriteLine("No global layers found");
                }
            }

            static bool ContainsAll(ReadOnlySpan<ASCIIText256> a, ReadOnlySpan<ASCIIText256> b)
            {
                foreach (ASCIIText256 layer in b)
                {
                    bool contains = false;
                    foreach (ASCIIText256 availableLayer in a)
                    {
                        if (availableLayer == layer)
                        {
                            contains = true;
                            break;
                        }
                    }

                    if (!contains)
                    {
                        return false;
                    }
                }

                return true;
            }
#endif

            using Array<ASCIIText256> globalExtensions = library.GetGlobalExtensions();
            using List<ASCIIText256> inputExtensions = new(extensions);
            foreach (ASCIIText256 extensionName in globalExtensions)
            {
                if (extensionName == new ASCIIText256(VK_EXT_DEBUG_UTILS_EXTENSION_NAME))
                {
                    inputExtensions.Add(extensionName);
                }
                else if (extensionName == new ASCIIText256(VK_EXT_SWAPCHAIN_COLOR_SPACE_EXTENSION_NAME))
                {
                    inputExtensions.Add(extensionName);
                }
            }

            Span<byte> applicationNameBytes = stackalloc byte[applicationName.Length];
            for (int i = 0; i < applicationName.Length; i++)
            {
                applicationNameBytes[i] = (byte)applicationName[i];
            }

            Span<byte> engineNameBytes = stackalloc byte[engineName.Length];
            for (int i = 0; i < engineName.Length; i++)
            {
                engineNameBytes[i] = (byte)engineName[i];
            }

            VkApplicationInfo appInfo = new();
            appInfo.pApplicationName = new VkUtf8ReadOnlyString(applicationNameBytes);
            appInfo.applicationVersion = VkVersion.Version_1_0;
            appInfo.pEngineName = new VkUtf8ReadOnlyString(engineNameBytes);
            appInfo.engineVersion = VkVersion.Version_1_0;
            appInfo.apiVersion = VkVersion.Version_1_3;

            using List<VkUtf8String> vkInstanceLayers = new(inputLayers.Count);
            using List<MemoryAddress> tempAllocations = new();
            Span<byte> nameBuffer = stackalloc byte[ASCIIText256.Capacity];
            foreach (ASCIIText256 instanceLayer in inputLayers)
            {
                int byteLength = instanceLayer.CopyTo(nameBuffer) + 1;
                MemoryAddress newAllocation = MemoryAddress.Allocate(byteLength);
                newAllocation.CopyFrom(nameBuffer.Slice(0, byteLength));
                vkInstanceLayers.Add(new(newAllocation.Pointer));
                tempAllocations.Add(newAllocation);
                nameBuffer.Clear();
            }

            using List<VkUtf8String> vkInstanceExtensions = new(inputExtensions.Count);
            foreach (ASCIIText256 instanceExtension in inputExtensions)
            {
                int byteLength = instanceExtension.CopyTo(nameBuffer) + 1;
                MemoryAddress newAllocation = MemoryAddress.Allocate(byteLength);
                newAllocation.CopyFrom(nameBuffer.Slice(0, byteLength));
                vkInstanceExtensions.Add(new(newAllocation.Pointer));
                tempAllocations.Add(newAllocation);
                nameBuffer.Clear();
            }

            using VkStringArray layerNames = new(vkInstanceLayers);
            using VkStringArray extensionNames = new(vkInstanceExtensions);

            VkInstanceCreateInfo createInfo = new();
            createInfo.pApplicationInfo = &appInfo;
            createInfo.enabledLayerCount = layerNames.Length;
            createInfo.ppEnabledLayerNames = layerNames;
            createInfo.enabledExtensionCount = extensionNames.Length;
            createInfo.ppEnabledExtensionNames = extensionNames;

            VkDebugUtilsMessengerCreateInfoEXT debugUtilsCreateInfo = new();
            if (inputLayers.Count > 0)
            {
                createInfo.pNext = &debugUtilsCreateInfo;
                debugUtilsCreateInfo.messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Error | VkDebugUtilsMessageSeverityFlagsEXT.Warning;
                debugUtilsCreateInfo.messageType = VkDebugUtilsMessageTypeFlagsEXT.General | VkDebugUtilsMessageTypeFlagsEXT.Validation;
                debugUtilsCreateInfo.pfnUserCallback = &DebugMessengerCallback;
            }

            //create vulkan instance
            VkResult result = vkCreateInstance(&createInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create instance: {result}");
            }

            foreach (MemoryAddress allocation in tempAllocations)
            {
                allocation.Dispose();
            }

            this.applicationName = new(applicationName);
            this.engineName = new(engineName);
            vkLoadInstanceOnly(value);
            valid = true;

            if (inputLayers.Count > 0)
            {
                result = vkCreateDebugUtilsMessengerEXT(value, &debugUtilsCreateInfo, null, out debugMessenger);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to create debug messenger: {result}");
                }
            }

            //find physical devices
            uint physicalDeviceCount = 0;
            result = vkEnumeratePhysicalDevices(value, &physicalDeviceCount, null);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate physical devices: {result}");
            }

            if (physicalDeviceCount == 0)
            {
                throw new PlatformNotSupportedException("No physical devices found to render to");
            }

            physicalDevices = new((int)physicalDeviceCount);
            VkPhysicalDevice* physicalDevicesPointer = stackalloc VkPhysicalDevice[(int)physicalDeviceCount];
            result = vkEnumeratePhysicalDevices(value, &physicalDeviceCount, physicalDevicesPointer);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate physical devices: {result}");
            }

            for (int i = 0; i < physicalDeviceCount; i++)
            {
                physicalDevices[i] = new(physicalDevicesPointer[i]);
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            applicationName.Dispose();
            engineName.Dispose();
            physicalDevices.Dispose();

            if (debugMessenger != default(VkDebugUtilsMessengerEXT))
            {
                vkDestroyDebugUtilsMessengerEXT(value, debugMessenger);
            }

            valid = false;
            vkDestroyInstance(value);
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (!valid)
            {
                throw new ObjectDisposedException(nameof(Instance));
            }
        }

        public readonly bool TryGetBestPhysicalDevice(ReadOnlySpan<ASCIIText256> requiredExtensions, out PhysicalDevice device)
        {
            uint highestScore = 0;
            device = default;
            for (int i = 0; i < physicalDevices.Length; i++)
            {
                uint score = GetScore(physicalDevices[i], requiredExtensions);
                if (score > highestScore)
                {
                    highestScore = score;
                    device = physicalDevices[i];
                }
            }

            return device != default;

            static unsafe uint GetScore(PhysicalDevice physicalDevice, ReadOnlySpan<ASCIIText256> requiredExtensions)
            {
                VkPhysicalDeviceFeatures features = physicalDevice.GetFeatures();
                if (!features.geometryShader)
                {
                    //no geometry shader support
                    return 0;
                }

                if (!physicalDevice.TryGetGraphicsQueueFamily(out _))
                {
                    //no ability to render
                    return 0;
                }

                ReadOnlySpan<VkExtensionProperties> availableExtensions = physicalDevice.GetExtensions();
                if (availableExtensions.Length > 0)
                {
                    foreach (ASCIIText256 requiredExtension in requiredExtensions)
                    {
                        bool isAvailable = false;
                        foreach (VkExtensionProperties extension in availableExtensions)
                        {
                            ASCIIText256 extensionName = new(extension.extensionName);
                            if (extensionName == requiredExtension)
                            {
                                isAvailable = true;
                                break;
                            }
                        }

                        if (!isAvailable)
                        {
                            //required extensions missing
                            return 0;
                        }
                    }
                }
                else if (requiredExtensions.Length > 0)
                {
                    //required extensions missing
                    return 0;
                }

                VkPhysicalDeviceProperties properties = physicalDevice.GetProperties();
                uint score = properties.limits.maxImageDimension2D;
                if (properties.deviceType == VkPhysicalDeviceType.DiscreteGpu)
                {
                    //discrete gpus greatly preferred
                    score *= 1024;
                }

                return score;
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Instance instance && Equals(instance);
        }

        public readonly bool Equals(Instance other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(Instance left, Instance right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Instance left, Instance right)
        {
            return !(left == right);
        }

        [UnmanagedCallersOnly]
        private static uint DebugMessengerCallback(VkDebugUtilsMessageSeverityFlagsEXT messageSeverity, VkDebugUtilsMessageTypeFlagsEXT messageTypes, VkDebugUtilsMessengerCallbackDataEXT* pCallbackData, void* userData)
        {
            string str = new((sbyte*)pCallbackData->pMessage);
            if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Error)
            {
                throw new Exception(str);
            }

            if (messageTypes == VkDebugUtilsMessageTypeFlagsEXT.Validation)
            {
                Trace.WriteLine($"[Vulkan]: Validation: {messageSeverity} - {str}");
            }
            else
            {
                Trace.WriteLine($"[Vulkan]: {messageSeverity} - {str}");
            }

            return VK_FALSE;
        }
    }
}
