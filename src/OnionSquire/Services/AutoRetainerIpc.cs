using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace OnionSquire.Services;

/// <summary>
/// IPC Subscriber bridging OnionSquire with AutoRetainer.
/// Uses Dalamud IPC so OnionSquire does not need hard assembly links at compile time.
/// </summary>
public class AutoRetainerIpc : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;

    private readonly ICallGateSubscriber<bool> isReadySubscriber;
    private readonly ICallGateSubscriber<ulong[]> getRegisteredCharactersSubscriber;

    public bool IsAvailable
    {
        get
        {
            try
            {
                return isReadySubscriber.InvokeFunc();
            }
            catch
            {
                return false;
            }
        }
    }

    public AutoRetainerIpc(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        isReadySubscriber = pluginInterface.GetIpcSubscriber<bool>("AutoRetainer.Ready");
        getRegisteredCharactersSubscriber = pluginInterface.GetIpcSubscriber<ulong[]>("AutoRetainer.GetRegisteredCharacters");
    }

    /// <summary>
    /// Gets registered character ContentIds managed by AutoRetainer.
    /// </summary>
    public ulong[] GetRegisteredCharacters()
    {
        if (!IsAvailable) return Array.Empty<ulong>();
        try
        {
            return getRegisteredCharactersSubscriber.InvokeFunc();
        }
        catch
        {
            return Array.Empty<ulong>();
        }
    }

    /// <summary>
    /// Requests AutoRetainer to hold/postprocess retainers.
    /// </summary>
    public void RequestRetainerPostprocess()
    {
        if (!IsAvailable) return;
        try
        {
            pluginInterface.GetIpcSubscriber<object>("AutoRetainer.RequestRetainerPostprocess").InvokeAction();
        }
        catch
        {
        }
    }

    /// <summary>
    /// Tells AutoRetainer that OnionSquire is done postprocessing.
    /// </summary>
    public void FinishRetainerPostProcess()
    {
        if (!IsAvailable) return;
        try
        {
            pluginInterface.GetIpcSubscriber<object>("AutoRetainer.FinishRetainerPostProcess").InvokeAction();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
    }
}
