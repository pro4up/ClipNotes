using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClipNotes.Models;

namespace ClipNotes.Services;

public class HotkeyService : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, HotkeyAction> _registeredHotkeys = new();
    private int _nextId = 9000;

    public event Action<HotkeyAction>? HotkeyPressed;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public void RegisterHotkeys(IEnumerable<HotkeyBinding> bindings)
    {
        UnregisterAll();
        foreach (var b in bindings)
        {
            if (b.Key == Key.None) continue;

            var id = _nextId++;
            uint mod = 0;
            if (b.Modifiers.HasFlag(ModifierKeys.Alt)) mod |= 0x0001;
            if (b.Modifiers.HasFlag(ModifierKeys.Control)) mod |= 0x0002;
            if (b.Modifiers.HasFlag(ModifierKeys.Shift)) mod |= 0x0004;
            mod |= 0x4000; // MOD_NOREPEAT

            var vk = (uint)KeyInterop.VirtualKeyFromKey(b.Key);

            if (RegisterHotKey(_hwnd, id, mod, vk))
                _registeredHotkeys[id] = b.Action;
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _registeredHotkeys.Keys)
            UnregisterHotKey(_hwnd, id);
        _registeredHotkeys.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_registeredHotkeys.TryGetValue(id, out var action))
            {
                HotkeyPressed?.Invoke(action);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
