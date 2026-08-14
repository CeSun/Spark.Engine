using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Core.Contexts;
using Silk.NET.WebGPU;
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

        var adapter = RequestAdapter(api, instance);
        var device = RequestDevice(api, adapter);
        var queue = api.DeviceGetQueue(device);

        builder.Services.AddSingleton(new WebGPUContext(api, instance, adapter, device, queue));

        return builder;
    }

    private static unsafe Adapter* RequestAdapter(WebGPU api, Instance* instance)
    {
        Adapter* adapter = null;
        string? error = null;
        using var signal = new ManualResetEventSlim(false);

        RequestAdapterCallback callback = (status, result, message, _) =>
        {
            if (status == RequestAdapterStatus.Success)
            {
                adapter = result;
            }
            else
            {
                error = $"WebGPU adapter request failed: {status}";
            }

            signal.Set();
        };

        api.InstanceRequestAdapter(instance, null, callback, null);
        signal.Wait();
        GC.KeepAlive(callback);

        if (adapter == null)
            throw new InvalidOperationException(error ?? "No WebGPU adapter available.");

        return adapter;
    }

    private static unsafe Device* RequestDevice(WebGPU api, Adapter* adapter)
    {
        Device* device = null;
        string? error = null;
        using var signal = new ManualResetEventSlim(false);

        RequestDeviceCallback callback = (status, result, message, _) =>
        {
            if (status == RequestDeviceStatus.Success)
            {
                device = result;
            }
            else
            {
                error = $"WebGPU device request failed: {status}";
            }

            signal.Set();
        };

        api.AdapterRequestDevice(adapter, null, callback, null);
        signal.Wait();
        GC.KeepAlive(callback);

        if (device == null)
            throw new InvalidOperationException(error ?? "No WebGPU device available.");

        return device;
    }
}

public unsafe class WebGPUContext
{
    public WebGPU Api { get; }

    public Instance* Instance { get; }

    public Adapter* Adapter { get; }

    public Device* Device { get; }

    public Queue* Queue { get; }

    public WebGPUContext(WebGPU api, Instance* instance, Adapter* adapter, Device* device, Queue* queue)
    {
        Api = api;
        Instance = instance;
        Adapter = adapter;
        Device = device;
        Queue = queue;
    }

    public Surface* CreateSurface(INativeWindowSource nativeWindow)
    {
        return WebGPUSurface.CreateWebGPUSurface(nativeWindow, Api, Instance);
    }
}
