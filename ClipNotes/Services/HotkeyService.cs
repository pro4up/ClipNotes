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
    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_HOTKEY = 0x0312;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, HotkeyAction> _registeredHotkeys = new();
    private readonly Dictionary<uint, HotkeyAction> _vkToMarkerAction = new();
    private int _nextId = 9000;

    private IntPtr _llHook = IntPtr.Zero;
    private LowLevelKeyboardProc? _llProc; // keep reference to prevent GC

    public event Action<HotkeyAction>? HotkeyPressed;
    public event Action<HotkeyAction>? HotkeyReleased;

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
        _vkToMarkerAction.Clear();

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
            {
                _registeredHotkeys[id] = b.Action;

                // Track marker keys for hold-mode KeyUp detection
                if (b.Action is HotkeyAction.MarkerBug or HotkeyAction.MarkerTask or HotkeyAction.MarkerNote or HotkeyAction.MarkerSummary)
                    _vkToMarkerAction[vk] = b.Action;
            }
        }
    }

    public void EnableHoldMode()
    {
        if (_llHook != IntPtr.Zero) return;
        _llProc = LowLevelHookProc;
        var hMod = GetModuleHandle(null);
        _llHook = SetWindowsHookEx(WH_KEYBOARD_LL, _llProc, hMod, 0);
    }

    public void DisableHoldMode()
    {
        if (_llHook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_llHook);
        _llHook = IntPtr.Zero;
        _llProc = null;
    }

    private IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
        {
            var vk = (uint)Marshal.ReadInt32(lParam);
            if (_vkToMarkerAction.TryGetValue(vk, out var action))
                HotkeyReleased?.Invoke(action);
        }
        return CallNextHookEx(_llHook, nCode, wParam, lParam);
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
        DisableHoldMode();
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
