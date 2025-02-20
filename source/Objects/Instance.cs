using Collections;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Instance : IDisposable, IEquatable<Instance>
    {
        private static readonly FixedString[] preferredValidationLayers =
        [
            "VK_LAYER_KHRONOS_validation"
        ];

        private static readonly FixedString[] fallbackValidationLayers =
        [
            "VK_LAYER_LUNARG_standard_validation"
        ];

        private static readonly FixedString[] fallbackIndividualLayers =
        [
            "VK_LAYER_GOOGLE_threading",
            "VK_LAYER_LUNARG_parameter_validation",
            "VK_LAYER_LUNARG_object_tracker",
            "VK_LAYER_LUNARG_core_validation",
            "VK_LAYER_GOOGLE_unique_objects",
        ];

        private static readonly FixedString[] fallbackFallbackLayers =
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

        public readonly USpan<PhysicalDevice> PhysicalDevices
        {
            get
            {
                ThrowIfDisposed();

                return physicalDevices.AsSpan();
            }
        }

        public readonly bool IsDisposed => !valid;

        public readonly USpan<char> ApplicationName
        {
            get
            {
                ThrowIfDisposed();

                return applicationName.AsSpan();
            }
        }

        public readonly USpan<char> EngineName
        {
            get
            {
                ThrowIfDisposed();

                return engineName.AsSpan();
            }
        }

        internal Instance(Library library, USpan<char> applicationName, USpan<char> engineName, USpan<FixedString> extensions)
        {
            using List<FixedString> inputLayers = new();

#if DEBUG
            using Array<FixedString> globalLayers = library.GetGlobalLayers();
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
                    USpan<char> buffer = stackalloc char[FixedString.Capacity];
                    foreach (FixedString layer in globalLayers)
                    {
                        uint length = layer.CopyTo(buffer);
                        remaining.Append(buffer.Slice(0, length));
                        remaining.Append(',');
                        remaining.Append(' ');
                    }

                    remaining.RemoveAt(remaining.Length - 1);
                    remaining.RemoveAt(remaining.Length - 1);
                    Trace.WriteLine($"No suitable validation layers found, there were instead:\n{remaining}");
                }
                else
                {
                    Trace.WriteLine("No global layers found");
                }
            }

            static bool ContainsAll(USpan<FixedString> a, USpan<FixedString> b)
            {
                foreach (FixedString layer in b)
                {
                    bool contains = false;
                    foreach (FixedString availableLayer in a)
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

            using Array<FixedString> globalExtensions = library.GetGlobalExtensions();
            using List<FixedString> inputExtensions = new(extensions);
            foreach (FixedString extensionName in globalExtensions)
            {
                if (extensionName == new FixedString(VK_EXT_DEBUG_UTILS_EXTENSION_NAME))
                {
                    inputExtensions.Add(extensionName);
                }
                else if (extensionName == new FixedString(VK_EXT_SWAPCHAIN_COLOR_SPACE_EXTENSION_NAME))
                {
                    inputExtensions.Add(extensionName);
                }
            }

            USpan<byte> applicationNameBytes = stackalloc byte[(int)applicationName.Length];
            for (uint i = 0; i < applicationName.Length; i++)
            {
                applicationNameBytes[i] = (byte)applicationName[i];
            }

            USpan<byte> engineNameBytes = stackalloc byte[(int)engineName.Length];
            for (uint i = 0; i < engineName.Length; i++)
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
            using List<nint> tempAllocations = new();
            USpan<byte> nameBuffer = stackalloc byte[FixedString.Capacity];
            foreach (FixedString instanceLayer in inputLayers)
            {
                uint length = instanceLayer.CopyTo(nameBuffer) + 1;
                byte* newAllocation = (byte*)Allocations.Allocate(length);
                Unsafe.CopyBlock(newAllocation, (void*)nameBuffer.Address, length);
                vkInstanceLayers.Add(new(newAllocation));
                tempAllocations.Add((nint)newAllocation);
                nameBuffer.Clear();
            }

            using List<VkUtf8String> vkInstanceExtensions = new(inputExtensions.Count);
            foreach (FixedString instanceExtension in inputExtensions)
            {
                uint length = instanceExtension.CopyTo(nameBuffer) + 1;
                byte* newAllocation = (byte*)Allocations.Allocate(length);
                Unsafe.CopyBlock(newAllocation, (void*)nameBuffer.Address, length);
                vkInstanceExtensions.Add(new(newAllocation));
                tempAllocations.Add((nint)newAllocation);
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

            foreach (nint allocation in tempAllocations)
            {
                void* pointer = (void*)allocation;
                Allocations.Free(ref pointer);
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

            physicalDevices = new(physicalDeviceCount);
            VkPhysicalDevice* physicalDevicesPointer = stackalloc VkPhysicalDevice[(int)physicalDeviceCount];
            result = vkEnumeratePhysicalDevices(value, &physicalDeviceCount, physicalDevicesPointer);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate physical devices: {result}");
            }

            for (uint i = 0; i < physicalDeviceCount; i++)
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
