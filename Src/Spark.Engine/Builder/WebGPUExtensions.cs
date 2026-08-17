using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Core.Contexts;
using Silk.NET.WebGPU;
using Spark.Engine.Render.Common;
using System;
using System.Threading;

namespace Spark.Engine.Builder;

public static class WebGPUExtensions
{
    public static unsafe EngineBuilder InitializeWebGPU(this EngineBuilder builder)
    {
        var api = WebGPU.GetApi();

        var instanceDescriptor = new InstanceDescriptor();
        var instance = api.CreateInstance(ref instanceDescriptor);

        builder.Services.AddSingleton(new WebGPUContext(api, instance));

        return builder;
    }
}

public unsafe class WebGPUContext
{
    public WebGPU Api { get; }

    public Instance* Instance { get; }

    public Adapter* Adapter { get; private set; }

    public Device* Device { get; private set; }

    public Queue* Queue { get; private set; }

    public WebGPUContext(WebGPU api, Instance* instance)
    {
        Api = api;
        Instance = instance;
    }

    public RenderSurface CreateSurface(INativeWindowSource nativeWindow)
    {
        var surface = WebGPUSurface.CreateWebGPUSurface(nativeWindow, Api, Instance);
        EnsureDevice(surface);
        return new RenderSurface(Api, Adapter, Device, surface);
    }

    private void EnsureDevice(Surface* compatibleSurface)
    {
        if (Device != null)
            return;

        Adapter = RequestAdapter(compatibleSurface);
        Device = RequestDevice(Adapter);
        Queue = Api.DeviceGetQueue(Device);
    }

    private Adapter* RequestAdapter(Surface* compatibleSurface)
    {
        Adapter* adapter = null;
        string? error = null;
        using var signal = new ManualResetEventSlim(false);

        RequestAdapterCallback callback = (status, result, message, _) =>
        {
            if (status == RequestAdapterStatus.Success)
                adapter = result;
            else
                error = $"WebGPU adapter request failed: {status}";

            signal.Set();
        };

        var options = new RequestAdapterOptions { CompatibleSurface = compatibleSurface };
        Api.InstanceRequestAdapter(Instance, ref options, callback, null);
        signal.Wait();
        GC.KeepAlive(callback);

        if (adapter == null)
            throw new InvalidOperationException(error ?? "No WebGPU adapter available.");

        return adapter;
    }

    private Device* RequestDevice(Adapter* adapter)
    {
        Device* device = null;
        string? error = null;
        using var signal = new ManualResetEventSlim(false);

        RequestDeviceCallback callback = (status, result, message, _) =>
        {
            if (status == RequestDeviceStatus.Success)
                device = result;
            else
                error = $"WebGPU device request failed: {status}";

            signal.Set();
        };

        var descriptor = new DeviceDescriptor();
        Api.AdapterRequestDevice(adapter, ref descriptor, callback, null);
        signal.Wait();
        GC.KeepAlive(callback);

        if (device == null)
            throw new InvalidOperationException(error ?? "No WebGPU device available.");

        return device;
    }
}
