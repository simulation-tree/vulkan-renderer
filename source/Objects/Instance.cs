using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unmanaged;
using Unmanaged.Collections;
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

        private readonly UnmanagedArray<PhysicalDevice> physicalDevices;
        private readonly VkInstance value;
        private readonly UnmanagedArray<char> applicationName;
        private readonly UnmanagedArray<char> engineName;
        private bool valid;

        public readonly VkInstance Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly nint Address => (nint)value.Handle;

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

        internal Instance(ReadOnlySpan<char> applicationName, ReadOnlySpan<char> engineName, IEnumerable<FixedString>? extensions = null)
        {
            uint count = 0;
            VkResult result = vkEnumerateInstanceLayerProperties(&count, null);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate instance layer properties: {result}");
            }

            //gather instance layers
            using UnmanagedList<FixedString> instanceLayers = UnmanagedList<FixedString>.Create();
            if (count > 0)
            {
                VkLayerProperties* properties = stackalloc VkLayerProperties[(int)count];
                result = vkEnumerateInstanceLayerProperties(&count, properties);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to enumerate instance layer properties: {result}");
                }

                using UnmanagedArray<FixedString> availableInstanceLayers = new(count);
                for (uint i = 0; i < count; i++)
                {
                    availableInstanceLayers[i] = new(properties[i].layerName);
                }

                if (ContainsAll(availableInstanceLayers, preferredValidationLayers))
                {
                    instanceLayers.AddRange(preferredValidationLayers);
                }
                else if (ContainsAll(availableInstanceLayers, fallbackValidationLayers))
                {
                    instanceLayers.AddRange(fallbackValidationLayers);
                }
                else if (ContainsAll(availableInstanceLayers, fallbackIndividualLayers))
                {
                    instanceLayers.AddRange(fallbackIndividualLayers);
                }
                else if (ContainsAll(availableInstanceLayers, fallbackFallbackLayers))
                {
                    instanceLayers.AddRange(fallbackFallbackLayers);
                }
                else
                {
                    if (availableInstanceLayers.Length > 0)
                    {
                        using UnmanagedList<char> remaining = UnmanagedList<char>.Create();
                        Span<char> buffer = stackalloc char[FixedString.MaxLength];
                        foreach (FixedString layer in availableInstanceLayers)
                        {
                            int length = layer.CopyTo(buffer);
                            remaining.AddRange(buffer[..length]);
                            remaining.AddRange(", ");
                        }

                        remaining.RemoveAt(remaining.Count - 1);
                        remaining.RemoveAt(remaining.Count - 1);
                        Debug.WriteLine("No suitable validation layers found, there were instead:\n" + remaining);
                    }
                    else
                    {
                        Debug.WriteLine("No validation layers found");
                    }
                }

                static bool ContainsAll(IReadOnlyList<FixedString> a, IReadOnlyList<FixedString> b)
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
            }

            //find vk_ext_debug_utils
            uint extensionCount = 0;
            result = vkEnumerateInstanceExtensionProperties(&extensionCount, null);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate instance extension properties: {result}");
            }

            using UnmanagedList<FixedString> instanceExtensions = UnmanagedList<FixedString>.Create();
            if (extensions is not null)
            {
                foreach (FixedString extension in extensions)
                {
                    instanceExtensions.Add(extension);
                }
            }

            if (extensionCount > 0)
            {
                VkExtensionProperties* extensionProperties = stackalloc VkExtensionProperties[(int)extensionCount];
                result = vkEnumerateInstanceExtensionProperties(&extensionCount, extensionProperties);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to enumerate instance extension properties: {result}");
                }

                for (int i = 0; i < extensionCount; i++)
                {
                    FixedString extensionName = new(extensionProperties[i].extensionName);
                    if (extensionName == new FixedString(VK_EXT_DEBUG_UTILS_EXTENSION_NAME))
                    {
                        instanceExtensions.Add(extensionName);
                    }
                    else if (extensionName == new FixedString(VK_EXT_SWAPCHAIN_COLOR_SPACE_EXTENSION_NAME))
                    {
                        instanceExtensions.Add(extensionName);
                    }
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

            using UnmanagedList<VkUtf8String> vkInstanceLayers = new(instanceLayers.Count);
            Span<byte> nameBuffer = stackalloc byte[FixedString.MaxLength];
            foreach (FixedString instanceLayer in instanceLayers)
            {
                int length = instanceLayer.CopyTo(nameBuffer);
                fixed (byte* bytes = nameBuffer)
                {
                    vkInstanceLayers.Add(new(bytes));
                }
            }

            using VkStringArray layerNames = new(vkInstanceLayers);
            using UnmanagedList<VkUtf8String> vkInstanceExtensions = new(instanceExtensions.Count);
            foreach (FixedString instanceExtension in instanceExtensions)
            {
                int length = instanceExtension.CopyTo(nameBuffer);
                fixed (byte* bytes = nameBuffer)
                {
                    byte* pointer = (byte*)NativeMemory.Alloc((nuint)length);
                    System.Runtime.CompilerServices.Unsafe.CopyBlock(pointer, bytes, (uint)length);
                    vkInstanceExtensions.Add(new(pointer));
                }
            }

            using VkStringArray extensionNames = new(vkInstanceExtensions);

            VkInstanceCreateInfo createInfo = new();
            createInfo.pApplicationInfo = &appInfo;
            createInfo.enabledLayerCount = layerNames.Length;
            createInfo.ppEnabledLayerNames = layerNames;
            createInfo.enabledExtensionCount = extensionNames.Length;
            createInfo.ppEnabledExtensionNames = extensionNames;

            VkDebugUtilsMessengerCreateInfoEXT debugUtilsCreateInfo = new();
            if (instanceLayers.Count > 0)
            {
                createInfo.pNext = &debugUtilsCreateInfo;
                debugUtilsCreateInfo.messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Error | VkDebugUtilsMessageSeverityFlagsEXT.Warning;
                debugUtilsCreateInfo.messageType = VkDebugUtilsMessageTypeFlagsEXT.General | VkDebugUtilsMessageTypeFlagsEXT.Validation;
                debugUtilsCreateInfo.pfnUserCallback = &DebugMessengerCallback;
            }

            //create vulkan instance
            result = vkCreateInstance(&createInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create instance: {result}");
            }

            this.applicationName = new(applicationName);
            this.engineName = new(engineName);
            vkLoadInstanceOnly(value);
            valid = true;

            if (instanceLayers.Count > 0)
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
            foreach (PhysicalDevice physicalDevice in physicalDevices)
            {
                physicalDevice.Dispose();
            }

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

        public readonly bool TryPickDevice(Surface surface, out PhysicalDevice chosenPhysicalDevice)
        {
            ThrowIfDisposed();
            chosenPhysicalDevice = default;
            bool found = false;
            foreach (PhysicalDevice physicalDevice in physicalDevices)
            {
                (uint graphics, uint present) = surface.GetQueueFamily(physicalDevice);
                if (graphics == VK_QUEUE_FAMILY_IGNORED) continue;
                if (present == VK_QUEUE_FAMILY_IGNORED) continue;

                SwapchainCapabilities swapchainInfo = surface.GetSwapchainInfo(physicalDevice);
                if (swapchainInfo.formats.Length == 0) continue;
                if (swapchainInfo.presentModes.Length == 0) continue;

                VkPhysicalDeviceFeatures features = physicalDevice.GetFeatures();
                if (!features.samplerAnisotropy) continue;

                VkPhysicalDeviceProperties properties = physicalDevice.GetProperties();
                if (properties.deviceType == VkPhysicalDeviceType.DiscreteGpu)
                {
                    chosenPhysicalDevice = physicalDevice;
                    found = true;
                    break;
                }
                else if (!found)
                {
                    chosenPhysicalDevice = physicalDevice;
                    found = true;
                }
            }

            return found;
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
            FixedString message = new(pCallbackData->pMessage);
            if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Error)
            {
                throw new Exception(message.ToString());
            }

            if (messageTypes == VkDebugUtilsMessageTypeFlagsEXT.Validation)
            {
                Debug.WriteLine($"[Vulkan]: Validation: {messageSeverity} - {message}");
            }
            else
            {
                Debug.WriteLine($"[Vulkan]: {messageSeverity} - {message}");
            }

            return VK_FALSE;
        }

        public static void EnumerateInstanceExtensions()
        {
            //if instances can be created at runtime, then they will be
            //the extensions that will be needed will be fetched before hand,
            //this means that the destination exists first, then the renderer

            //but how is vulkan specified to be made, when a destination is created?
            //and how can an os window become a destination that has some renderer specified (like vulkan)?

            //the renderer mechanism will be attached to all os windows created here automatically
            //that will be the behaviour of rendering, its like a mod for projects that have
            //render destinations like windows

            //so rendering is a dependency for windows? i guess what else is a window going to have inside of them
            //rendering comes first, windows are a destination, so that tracks
        }
    }
}
