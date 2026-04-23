using System.Net;
using Arbor.HttpClient.Desktop.ViewModels;
using AwesomeAssertions;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class CookieJarViewModelTests
{
    [Fact]
    public void Constructor_WithNullContainer_ShouldInitializeWithEmptyCookieList()
    {
        var viewModel = new CookieJarViewModel(null);

        viewModel.Cookies.Should().BeEmpty();
        viewModel.Title.Should().Be("Cookie Jar");
        viewModel.Id.Should().Be("cookie-jar");
    }

    [Fact]
    public void Constructor_WithContainerHavingCookies_ShouldLoadCookies()
    {
        var container = new CookieContainer();
        container.Add(new Cookie("session", "abc123", "/", "example.com"));

        var viewModel = new CookieJarViewModel(container);

        viewModel.Cookies.Should().ContainSingle();
        viewModel.Cookies[0].Name.Should().Be("session");
        viewModel.Cookies[0].Value.Should().Be("abc123");
        viewModel.Cookies[0].Domain.Should().Be("example.com");
    }

    [Fact]
    public void AddCookieCommand_WithValidInputs_ShouldAddCookieToContainerAndList()
    {
        var container = new CookieContainer();
        var viewModel = new CookieJarViewModel(container);

        viewModel.NewCookieName = "token";
        viewModel.NewCookieValue = "xyz";
        viewModel.NewCookieDomain = "api.example.com";
        viewModel.AddCookieCommand.Execute(null);

        viewModel.Cookies.Should().ContainSingle();
        viewModel.Cookies[0].Name.Should().Be("token");
        viewModel.Cookies[0].Value.Should().Be("xyz");
        viewModel.NewCookieName.Should().BeEmpty();
        viewModel.NewCookieValue.Should().BeEmpty();
        viewModel.NewCookieDomain.Should().BeEmpty();

        var allCookies = container.GetAllCookies();
        allCookies.Should().ContainSingle(c => c.Name == "token");
    }

    [Fact]
    public void AddCookieCommand_WithEmptyName_ShouldNotAddCookie()
    {
        var container = new CookieContainer();
        var viewModel = new CookieJarViewModel(container);

        viewModel.NewCookieName = string.Empty;
        viewModel.NewCookieValue = "xyz";
        viewModel.NewCookieDomain = "example.com";
        viewModel.AddCookieCommand.Execute(null);

        viewModel.Cookies.Should().BeEmpty();
    }

    [Fact]
    public void AddCookieCommand_WithEmptyDomain_ShouldNotAddCookie()
    {
        var container = new CookieContainer();
        var viewModel = new CookieJarViewModel(container);

        viewModel.NewCookieName = "token";
        viewModel.NewCookieValue = "xyz";
        viewModel.NewCookieDomain = string.Empty;
        viewModel.AddCookieCommand.Execute(null);

        viewModel.Cookies.Should().BeEmpty();
    }

    [Fact]
    public void RemoveCommand_ShouldExpireCookieAndRemoveFromList()
    {
        var container = new CookieContainer();
        container.Add(new Cookie("session", "abc123", "/", "example.com"));
        var viewModel = new CookieJarViewModel(container);

        var entry = viewModel.Cookies.Single();
        viewModel.RemoveCommand.Execute(entry);

        viewModel.Cookies.Should().BeEmpty();
        container.GetAllCookies().Single().Expired.Should().BeTrue();
    }

    [Fact]
    public void ClearAllCommand_ShouldExpireAllCookiesAndEmptyList()
    {
        var container = new CookieContainer();
        container.Add(new Cookie("a", "1", "/", "example.com"));
        container.Add(new Cookie("b", "2", "/", "example.com"));
        var viewModel = new CookieJarViewModel(container);

        viewModel.ClearAllCommand.Execute(null);

        viewModel.Cookies.Should().BeEmpty();
        container.GetAllCookies().Should().AllSatisfy(c => c.Expired.Should().BeTrue());
    }

    [Fact]
    public void RefreshCommand_ShouldReloadCookiesFromContainer()
    {
        var container = new CookieContainer();
        var viewModel = new CookieJarViewModel(container);
        viewModel.Cookies.Should().BeEmpty();

        container.Add(new Cookie("new-cookie", "value", "/", "example.com"));
        viewModel.RefreshCommand.Execute(null);

        viewModel.Cookies.Should().ContainSingle(c => c.Name == "new-cookie");
    }
}
