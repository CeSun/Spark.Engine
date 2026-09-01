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

public unsafe sealed class WebGPUContext : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public WebGPU Api { get; }

    public Instance* Instance { get; }

    public Adapter* Adapter { get; private set; }

    public Device* Device { get; private set; }

    public Queue* Queue { get; private set; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private int _disposed;

    public WebGPUContext(WebGPU api, Instance* instance)
    {
        Api = api;
        Instance = instance;
    }

    public RenderSurface CreateSurface(INativeWindowSource nativeWindow)
    {
        ThrowIfDisposed();
        var surface = WebGPUSurface.CreateWebGPUSurface(nativeWindow, Api, Instance);
        try
        {
            EnsureDevice(surface);
            return new RenderSurface(Api, Adapter, Device, surface);
        }
        catch
        {
            if (surface != null)
                Api.SurfaceRelease(surface);
            throw;
        }
    }

    private void EnsureDevice(Surface* compatibleSurface)
    {
        ThrowIfDisposed();
        if (Device != null)
            return;

        Adapter = RequestAdapter(compatibleSurface);
        try
        {
            Device = RequestDevice(Adapter);
            Queue = Api.DeviceGetQueue(Device);
        }
        catch
        {
            if (Adapter != null)
                Api.AdapterRelease(Adapter);
            Adapter = null;
            throw;
        }
    }

    private Adapter* RequestAdapter(Surface* compatibleSurface)
    {
        Adapter* adapter = null;
        string? error = null;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        RequestAdapterCallback callback = (status, result, message, _) =>
        {
            if (status == RequestAdapterStatus.Success)
                adapter = result;
            else
                error = $"WebGPU adapter request failed: {status}";

            completion.TrySetResult();
        };

        var options = new RequestAdapterOptions { CompatibleSurface = compatibleSurface };
        Api.InstanceRequestAdapter(Instance, ref options, callback, null);
        if (!completion.Task.Wait(RequestTimeout))
            throw new TimeoutException($"Timed out waiting for WebGPU adapter after {RequestTimeout.TotalSeconds:0}s.");
        GC.KeepAlive(callback);

        if (adapter == null)
            throw new InvalidOperationException(error ?? "No WebGPU adapter available.");

        return adapter;
    }

    private Device* RequestDevice(Adapter* adapter)
    {
        Device* device = null;
        string? error = null;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        RequestDeviceCallback callback = (status, result, message, _) =>
        {
            if (status == RequestDeviceStatus.Success)
                device = result;
            else
                error = $"WebGPU device request failed: {status}";

            completion.TrySetResult();
        };

        var descriptor = new DeviceDescriptor();
        Api.AdapterRequestDevice(adapter, ref descriptor, callback, null);
        if (!completion.Task.Wait(RequestTimeout))
            throw new TimeoutException($"Timed out waiting for WebGPU device after {RequestTimeout.TotalSeconds:0}s.");
        GC.KeepAlive(callback);

        if (device == null)
            throw new InvalidOperationException(error ?? "No WebGPU device available.");

        return device;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var queue = Queue;
        var device = Device;
        var adapter = Adapter;

        Queue = null;
        Device = null;
        Adapter = null;

        if (queue != null)
            Api.QueueRelease(queue);
        if (device != null)
            Api.DeviceRelease(device);
        if (adapter != null)
            Api.AdapterRelease(adapter);
        if (Instance != null)
            Api.InstanceRelease(Instance);
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(WebGPUContext));
    }
}
