#include <windows.h>
#include <shobjidl.h>
#include <cstdio>
#include <cstring>

// Diagnoses the registered COM command. --invoke opens the real naming dialog;
// it does not confirm creation or simulate an Explorer/user foreground gesture.
int main(int argc, char** argv) {
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    CLSID id;
    CLSIDFromString(L"{717FB03F-1F3A-4F61-A5D9-39E038AB1D67}", &id);
    IExplorerCommand* command = nullptr;
    HRESULT hr = CoCreateInstance(id, nullptr, CLSCTX_ALL, IID_IExplorerCommand,
                                 reinterpret_cast<void**>(&command));
    printf("activation=%08lx\n", static_cast<unsigned long>(hr));
    if (command) {
        LPWSTR title = nullptr;
        hr = command->GetTitle(nullptr, &title);
        printf("title-result=%08lx\n", static_cast<unsigned long>(hr));
        CoTaskMemFree(title);
        if (argc == 2 && std::strcmp(argv[1], "--invoke") == 0) {
            hr = command->Invoke(nullptr, nullptr);
            printf("invoke-result=%08lx\n", static_cast<unsigned long>(hr));
        }
        command->Release();
    }
    CoUninitialize();
    return FAILED(hr) ? 1 : 0;
}
