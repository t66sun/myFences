#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <shlguid.h>
#include <new>
#include <string>

// A native surrogate COM server: the WPF runtime is never loaded into Explorer.
static const CLSID CommandId = {0x717fb03f,0x1f3a,0x4f61,{0xa5,0xd9,0x39,0xe0,0x38,0xab,0x1d,0x67}};
static HMODULE module;
static LONG objects;
static std::wstring ModuleDirectory()
{
    wchar_t path[32768] = {}; GetModuleFileNameW(module, path, 32768);
    std::wstring folder(path); return folder.substr(0, folder.find_last_of(L"\\/"));
}
static std::wstring MenuTitle()
{
    // The packaged COM surrogate does not share the unpackaged app's HKCU view.
    // Read only the localized caption from the registered external directory.
    HANDLE file = CreateFileW((ModuleDirectory() + L"\\MyFences.DesktopMenu.title").c_str(), GENERIC_READ,
        FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return L"New MyFences group";
    wchar_t text[256] = {}; DWORD bytes = 0;
    const bool read = ReadFile(file, text, sizeof(text) - sizeof(wchar_t), &bytes, nullptr) != FALSE;
    CloseHandle(file);
    if (!read || bytes < sizeof(wchar_t)) return L"New MyFences group";
    return std::wstring(text + (text[0] == 0xfeff ? 1 : 0));
}
static void Probe(const wchar_t* stage)
{
    const auto folder = ModuleDirectory();
    if (GetFileAttributesW((folder + L"\\prototype-mode").c_str()) == INVALID_FILE_ATTRIBUTES) return;
    HANDLE file = CreateFileW((folder + L"\\prototype.log").c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return;
    std::wstring line = std::wstring(stage) + L"\r\n"; DWORD written;
    WriteFile(file, line.data(), static_cast<DWORD>(line.size() * sizeof(wchar_t)), &written, nullptr); CloseHandle(file);
}
static bool OnDesktop(IUnknown* site)
{
    if (!site) return false;
    IServiceProvider* services = nullptr;
    if (FAILED(site->QueryInterface(IID_IServiceProvider, reinterpret_cast<void**>(&services)))) return false;
    IFolderView* view = nullptr;
    HRESULT hr = services->QueryService(SID_SFolderView, IID_IFolderView, reinterpret_cast<void**>(&view)); services->Release();
    if (FAILED(hr)) return false;
    IPersistFolder2* folder = nullptr; hr = view->GetFolder(IID_IPersistFolder2, reinterpret_cast<void**>(&folder)); view->Release();
    if (FAILED(hr)) return false;
    PIDLIST_ABSOLUTE pidl = nullptr; hr = folder->GetCurFolder(&pidl); folder->Release();
    bool desktop = SUCCEEDED(hr) && pidl && pidl->mkid.cb == 0;
    CoTaskMemFree(pidl); return desktop;
}
class Command final : public IExplorerCommand, public IObjectWithSite
{
    LONG refs = 1;
    IUnknown* site = nullptr;
public:
    Command() { InterlockedIncrement(&objects); Probe(L"created"); }
    ~Command() { if (site) site->Release(); InterlockedDecrement(&objects); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** result) override
    { if (!result) return E_POINTER; *result = nullptr; if (IsEqualIID(id, IID_IUnknown) || IsEqualIID(id, IID_IExplorerCommand)) *result = static_cast<IExplorerCommand*>(this); else if (IsEqualIID(id, IID_IObjectWithSite)) *result = static_cast<IObjectWithSite*>(this); else return E_NOINTERFACE; AddRef(); return S_OK; }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs); }
    ULONG STDMETHODCALLTYPE Release() override { ULONG n = InterlockedDecrement(&refs); if (!n) delete this; return n; }
    HRESULT STDMETHODCALLTYPE SetSite(IUnknown* value) override { if (site) site->Release(); site = value; if (site) site->AddRef(); return S_OK; }
    HRESULT STDMETHODCALLTYPE GetSite(REFIID id, void** result) override { return site ? site->QueryInterface(id, result) : E_FAIL; }
    HRESULT STDMETHODCALLTYPE GetTitle(IShellItemArray*, LPWSTR* title) override
    { Probe(L"title"); return SHStrDupW(MenuTitle().c_str(), title); }
    HRESULT STDMETHODCALLTYPE GetIcon(IShellItemArray*, LPWSTR* icon) override
    { return SHStrDupW((ModuleDirectory() + L"\\MyFences.exe,0").c_str(), icon); }
    HRESULT STDMETHODCALLTYPE GetToolTip(IShellItemArray*, LPWSTR* tooltip) override { *tooltip = nullptr; return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetCanonicalName(GUID* id) override { *id = CommandId; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override
    { bool prototype = GetFileAttributesW((ModuleDirectory() + L"\\prototype-mode").c_str()) != INVALID_FILE_ATTRIBUTES; bool desktop = OnDesktop(site); Probe(desktop ? L"state-desktop" : L"state-other"); *state = (prototype || desktop) ? ECS_ENABLED : ECS_HIDDEN; return S_OK; }
    HRESULT STDMETHODCALLTYPE Invoke(IShellItemArray*, IBindCtx*) override
    {
        Probe(L"invoke");
        if (GetFileAttributesW((ModuleDirectory() + L"\\prototype-mode").c_str()) != INVALID_FILE_ATTRIBUTES) return S_OK;
        const auto exe = ModuleDirectory() + L"\\MyFences.exe";
        POINT point; if (!GetCursorPos(&point)) return HRESULT_FROM_WIN32(GetLastError());
        const auto args = L"--new-group " + std::to_wstring(point.x) + L" " + std::to_wstring(point.y);
        auto result = reinterpret_cast<INT_PTR>(ShellExecuteW(nullptr, L"open", exe.c_str(), args.c_str(), nullptr, SW_SHOWNORMAL));
        return result > 32 ? S_OK : HRESULT_FROM_WIN32(static_cast<DWORD>(result));
    }
    HRESULT STDMETHODCALLTYPE GetFlags(EXPCMDFLAGS* flags) override { *flags = ECF_DEFAULT; return S_OK; }
    HRESULT STDMETHODCALLTYPE EnumSubCommands(IEnumExplorerCommand** commands) override { *commands = nullptr; return E_NOTIMPL; }
};
class Factory final : public IClassFactory
{
    LONG refs = 1;
public:
    Factory() { InterlockedIncrement(&objects); }
    ~Factory() { InterlockedDecrement(&objects); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** result) override
    { if (!result) return E_POINTER; *result = nullptr; if (IsEqualIID(id, IID_IUnknown) || IsEqualIID(id, IID_IClassFactory)) { *result = static_cast<IClassFactory*>(this); AddRef(); return S_OK; } return E_NOINTERFACE; }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs); }
    ULONG STDMETHODCALLTYPE Release() override { ULONG n = InterlockedDecrement(&refs); if (!n) delete this; return n; }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID id, void** result) override
    { if (outer) return CLASS_E_NOAGGREGATION; auto object = new(std::nothrow) Command(); if (!object) return E_OUTOFMEMORY; auto hr = object->QueryInterface(id, result); object->Release(); return hr; }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override { if (lock) InterlockedIncrement(&objects); else InterlockedDecrement(&objects); return S_OK; }
};
extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID id, REFIID iid, void** result)
{ if (!IsEqualCLSID(id, CommandId)) return CLASS_E_CLASSNOTAVAILABLE; auto factory = new(std::nothrow) Factory(); if (!factory) return E_OUTOFMEMORY; auto hr = factory->QueryInterface(iid, result); factory->Release(); return hr; }
extern "C" HRESULT __stdcall DllCanUnloadNow() { return objects == 0 ? S_OK : S_FALSE; }
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) { if (reason == DLL_PROCESS_ATTACH) { module = instance; DisableThreadLibraryCalls(instance); } return TRUE; }
