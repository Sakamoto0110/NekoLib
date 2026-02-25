# PageNav – TODO (Timeout & Service Registration Fix)

## Goal
Fix Path A service registration issues by:
- removing early instantiation of platform-dependent services
- making timeout handling Path-A compliant
- enforcing correct lifecycle boundaries

---

## 1. Stop creating runtime services during bootstrap

- [ ] Remove any `new PageTimeoutController(...)`
- [ ] Remove any `new WinFormsPageTimeoutAdapter(...)`
- [ ] Remove timer / dispatcher / observer creation **outside** `ConfigureServices`
- [ ] Ensure no service is created before `PageNavBootstrap.Start()`

> Bootstrap must only register **capabilities**, not runtime behavior.

---

## 2. Introduce a Timeout Service Factory

- [ ] Create interface:

```csharp
public interface IPageTimeoutServiceFactory
{
    IPageTimeoutService Create(NavigationContext context);
}
