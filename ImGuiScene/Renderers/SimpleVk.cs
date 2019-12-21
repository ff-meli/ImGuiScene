using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Vulkan;

using static SDL2.SDL;

namespace ImGuiScene
{
    public class SimpleVk : IRenderer
    {
        public RendererFactory.RendererBackend Type => RendererFactory.RendererBackend.Vulkan;

        public Vector4 ClearColor { get; set; }

        public bool Debuggable { get; }

        private SimpleSDLWindow _window;
        private VkInstance _instance = VkInstance.Null;
        private VkDebugReportCallbackEXT _debugCallback = VkDebugReportCallbackEXT.Null;
        private VkPhysicalDevice _physicalDevice = VkPhysicalDevice.Null;
        private ulong _surface;
        private uint _queueFamily;
        private VkDevice _device = VkDevice.Null;
        private VkQueue _queue;
        private VkSwapchainKHR _swapchain = VkSwapchainKHR.Null;
        private VkImage[] _swapImages = null;

        internal SimpleVk(bool enableDebugging)
        {
            Debuggable = enableDebugging;
        }

        public void AttachToWindow(SimpleSDLWindow sdlWindow)
        {
            _window = sdlWindow;
            // get the layers and extensions we need to create the instance

            // sdl + vulkan isn't very friendly for having multiple sources, so there's a bit of mess to build a unified array
            var coreExts = GetInstanceExtensions();
            SDL_Vulkan_GetInstanceExtensions(_window.Window, out uint extCount, null);
            var exts = new IntPtr[extCount + coreExts.Length];
            SDL_Vulkan_GetInstanceExtensions(_window.Window, out extCount, exts);
            
            foreach (var ext in coreExts)
            {
                exts[extCount++] = Marshal.StringToHGlobalAnsi(ext);
            }

            var coreLayers = GetInstanceLayers();
            var layers = new IntPtr[coreLayers.Length];
            for (var i = 0; i < coreLayers.Length; i++)
            {
                layers[i] = Marshal.StringToHGlobalAnsi(coreLayers[i]);
            }

            // actually create the instance
            CreateInstance(exts, layers);
            Vk.LoadInstanceFunctionPointers(_instance);

            // free native mem we needed for creation
            for (int i = 0; i < coreExts.Length; i++)
            {
                Marshal.FreeHGlobal(exts[exts.Length - i - 1]);
            }
            foreach (var layer in layers)
            {
                Marshal.FreeHGlobal(layer);
            }

            CreateDebugCallback();

            if (SDL_Vulkan_CreateSurface(_window.Window, _instance.Handle, out _surface) != SDL_bool.SDL_TRUE)
            {
                throw new Exception("Failed to create SDL Vulkan surface");
            }

            CreatePhysicalDevice();
            CreateLogicalDevice();
            //Vk.LoadDeviceFunctionPointers(_device);

            CreateSwapchain();

            sdlWindow.Show();
        }

        public void Clear()
        {

        }

        public void Present()
        {

        }

        public unsafe TextureWrap CreateTexture(void* pixelData, int width, int height, int bytesPerPixel)
        {
            return null;
        }

        #region ImGui forwarding
        public void ImGui_Init()
        {
            //_backend.Init();
        }

        public void ImGui_Shutdown()
        {
            //_backend.Shutdown();
        }

        public void ImGui_NewFrame()
        {
            //_backend.NewFrame();
        }

        public void ImGui_RenderDrawData(ImGuiNET.ImDrawDataPtr drawData)
        {
            //_backend.RenderDrawData(drawData);
        }
        #endregion

        private unsafe void CreateInstance(IntPtr[] extensions, IntPtr[] layers)
        {
            fixed (void* pExts = extensions)
            fixed (void* pLayers = layers)
            {
                var createInfo = VkInstanceCreateInfo.New();
                if (extensions.Length > 0)
                {
                    createInfo.enabledExtensionCount = (uint)extensions.Length;
                    createInfo.ppEnabledExtensionNames = new IntPtr(pExts);
                }

                if (layers.Length > 0)
                {
                    createInfo.enabledLayerCount = (uint)layers.Length;
                    createInfo.ppEnabledLayerNames = new IntPtr(pLayers);
                }

                var res = Vk.vkCreateInstance(ref createInfo, IntPtr.Zero, out _instance);
                if (res != VkResult.Success)
                {
                    throw new Exception("Failed to create Vulkan instance: " + res);
                }
            }
        }

        private void CreateDebugCallback()
        {
            if (Debuggable)
            {
                var dbgCbInfo = VkDebugReportCallbackCreateInfoEXT.New();
                dbgCbInfo.flags = VkDebugReportFlagsEXT.ErrorEXT | VkDebugReportFlagsEXT.WarningEXT | VkDebugReportFlagsEXT.PerformanceWarningEXT | VkDebugReportFlagsEXT.InformationEXT;
                dbgCbInfo.pfnCallback = Marshal.GetFunctionPointerForDelegate<PFN_vkDebugReportCallbackEXT>(DebugCallback);

                var res = Vk.vkCreateDebugReportCallbackEXT(_instance, ref dbgCbInfo, IntPtr.Zero, out _debugCallback);
                if (res != VkResult.Success)
                {
                    throw new Exception("Failed to create Vulkan debug callback");
                }
            }
        }

        private void CreatePhysicalDevice()
        {
            Vk.vkEnumeratePhysicalDevices(_instance, out uint count, IntPtr.Zero);
            if (count == 0)
            {
                throw new Exception("No valid Vulkan devices");
            }

            var devices = new VkPhysicalDevice[count];
            Vk.vkEnumeratePhysicalDevices(_instance, out count, out devices[0]);

            // Try for a discrete gpu but take what we can get otherwise
            foreach (var device in devices)
            {
                //Vk.vkGetPhysicalDeviceFeatures(device, out VkPhysicalDeviceFeatures features);
                Vk.vkGetPhysicalDeviceProperties(device, out VkPhysicalDeviceProperties properties);
                if (properties.deviceType == VkPhysicalDeviceType.DiscreteGpu)
                {
                    _physicalDevice = device;
                    return;
                }
            }

            _physicalDevice = devices[0];
        }

        private unsafe void CreateLogicalDevice()
        {
            FindQueueFamilies();

            var exts = GetDeviceExtensions();
            var extensions = new IntPtr[exts.Length];
            for (var i = 0; i < exts.Length; i++)
            {
                extensions[i] = Marshal.StringToHGlobalAnsi(exts[i]);
            }

            var qCreateInfo = VkDeviceQueueCreateInfo.New();
            qCreateInfo.queueCount = 1;
            qCreateInfo.queueFamilyIndex = _queueFamily;
            float priority = 1f;
            qCreateInfo.pQueuePriorities = new IntPtr(&priority);

            var features = new VkPhysicalDeviceFeatures();

            fixed (void *pExts = extensions)
            {
                var deviceCreateInfo = VkDeviceCreateInfo.New();
                deviceCreateInfo.queueCreateInfoCount = 1;
                deviceCreateInfo.pQueueCreateInfos = new IntPtr(&qCreateInfo);
                deviceCreateInfo.pEnabledFeatures = new IntPtr(&features);
                if (exts.Length > 0)
                {
                    deviceCreateInfo.enabledExtensionCount = (uint)exts.Length;
                    deviceCreateInfo.ppEnabledExtensionNames = new IntPtr(pExts);
                }

                var res = Vk.vkCreateDevice(_physicalDevice, ref deviceCreateInfo, IntPtr.Zero, out _device);
                if (res != VkResult.Success)
                {
                    throw new Exception("Unable to create logical device");
                }
            }

            foreach (var ext in extensions)
            {
                Marshal.FreeHGlobal(ext);
            }

            Vk.vkGetDeviceQueue(_device, _queueFamily, 0, out _queue);
        }

        private void FindQueueFamilies()
        {
            Vk.vkGetPhysicalDeviceQueueFamilyProperties(_physicalDevice, out uint count, IntPtr.Zero);
            var queueFamilies = new VkQueueFamilyProperties[count];
            Vk.vkGetPhysicalDeviceQueueFamilyProperties(_physicalDevice, out count, out queueFamilies[0]);

            // Just try for a single queue for graphics and present
            // This is not the optimal configuration but it is by far the simplest
            for (int idx = 0; idx < queueFamilies.Length; idx++)
            {
                var qf = queueFamilies[idx];
                if (qf.queueCount > 0 && (qf.queueFlags & VkQueueFlags.Graphics) == VkQueueFlags.Graphics)
                {
                    Vk.vkGetPhysicalDeviceSurfaceSupportKHR(_physicalDevice, (uint)idx, _surface, out VkBool32 presentSupport);
                    if (presentSupport)
                    {
                        _queueFamily = (uint)idx;
                        return;
                    }
                }
            }

            throw new Exception("No suitable graphics and present queue found");
        }

        void CreateSwapchain()
        {
            var format = ChooseSurfaceFormat();
            var presentMode = ChoosePresentMode();

            SDL_GetWindowSize(_window.Window, out int winWidth, out int winHeight);
            Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(_physicalDevice, _surface, out VkSurfaceCapabilitiesKHR caps);

            var swapCreateInfo = VkSwapchainCreateInfoKHR.New();
            swapCreateInfo.surface = _surface;
            swapCreateInfo.presentMode = presentMode;
            swapCreateInfo.imageFormat = format.format;
            swapCreateInfo.imageColorSpace = format.colorSpace;
            swapCreateInfo.imageExtent = new VkExtent2D
            {
                width = (uint)winWidth,
                height = (uint)winHeight
            };
            swapCreateInfo.minImageCount = caps.minImageCount + 1;
            swapCreateInfo.imageArrayLayers = 1;
            swapCreateInfo.imageUsage = VkImageUsageFlags.ColorAttachment;
            swapCreateInfo.imageSharingMode = VkSharingMode.Exclusive;
            swapCreateInfo.queueFamilyIndexCount = 0;
            swapCreateInfo.preTransform = caps.currentTransform;
            swapCreateInfo.compositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueKHR;
            swapCreateInfo.clipped = true;

            var ret = Vk.vkCreateSwapchainKHR(_device, ref swapCreateInfo, IntPtr.Zero, out _swapchain);
            if (ret != VkResult.Success)
            {
                throw new Exception("Failed to create swapchain");
            }

            Vk.vkGetSwapchainImagesKHR(_device, _swapchain, out uint count, IntPtr.Zero);
            _swapImages = new VkImage[count];
            Vk.vkGetSwapchainImagesKHR(_device, _swapchain, out count, out _swapImages[0]);
        }

        private string[] GetInstanceExtensions()
        {
            var exts = new List<string>();
            if (Debuggable)
            {
                exts.Add(Ext.I.VK_EXT_debug_report);
            }

            return exts.ToArray();
        }

        private string[] GetInstanceLayers()
        {
            var layers = new List<string>();
            if (Debuggable)
            {
                layers.Add("VK_LAYER_LUNARG_standard_validation");
            }

            return layers.ToArray();
        }

        private string[] GetDeviceExtensions()
        {
            return new string[]
            {
                Ext.D.VK_KHR_swapchain
            };
        }

        private VkSurfaceFormatKHR ChooseSurfaceFormat()
        {
            Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(_physicalDevice, _surface, out uint count, IntPtr.Zero);
            var formats = new VkSurfaceFormatKHR[count];
            Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(_physicalDevice, _surface, out count, out formats[0]);

            VkSurfaceFormatKHR format = new VkSurfaceFormatKHR();
            if (formats.Length == 1 && formats[0].format == VkFormat.Undefined)
            {
                format = new VkSurfaceFormatKHR
                {
                    colorSpace = VkColorSpaceKHR.SrgbNonlinearKHR,
                    format = VkFormat.B8g8r8a8Unorm
                };
            }
            else
            {
                foreach (var fmt in formats)
                {
                    if (fmt.colorSpace == VkColorSpaceKHR.SrgbNonlinearKHR && fmt.format == VkFormat.B8g8r8a8Unorm)
                    {
                        format = fmt;
                        break;
                    }
                }
                if (format.format == VkFormat.Undefined)
                {
                    format = formats[0];
                }
            }

            return format;
        }

        private VkPresentModeKHR ChoosePresentMode()
        {
            Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(_physicalDevice, _surface, out uint count, IntPtr.Zero);
            var modes = new VkPresentModeKHR[count];
            Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(_physicalDevice, _surface, out count, out modes[0]);

            if (modes.Contains(VkPresentModeKHR.MailboxKHR))
            {
                return VkPresentModeKHR.MailboxKHR;
            }
            // TODO: tie in to vsync option
            else if (modes.Contains(VkPresentModeKHR.ImmediateKHR))
            {
                return VkPresentModeKHR.ImmediateKHR;
            }

            return VkPresentModeKHR.FifoKHR;
        }

        private VkBool32 DebugCallback(VkDebugReportFlagsEXT flags, VkDebugReportObjectTypeEXT objectType, ulong obj, UIntPtr loc, int messageCode, IntPtr pLayerPrefix, IntPtr pMessage, IntPtr pUserData)
        {
            string msg = Marshal.PtrToStringAnsi(pMessage);
            Console.WriteLine(msg);

            return VkBool32.False;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                if (_swapchain != VkSwapchainKHR.Null)
                {
                    Vk.vkDestroySwapchainKHR(_device, _swapchain, IntPtr.Zero);
                }

                if (_device != VkDevice.Null)
                {
                    Vk.vkDestroyDevice(_device, IntPtr.Zero);
                }

                if (_surface != 0)
                {
                    Vk.vkDestroySurfaceKHR(_instance, _surface, IntPtr.Zero);
                    _surface = 0;
                }

                if (Debuggable)
                {
                    Vk.vkDestroyDebugReportCallbackEXT(_instance, _debugCallback, IntPtr.Zero);
                }

                Vk.vkDestroyInstance(_instance, IntPtr.Zero);

                disposedValue = true;
            }
        }

        ~SimpleVk()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
