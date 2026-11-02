using learn_Assist.Models;
using learn_Assist.ViewModels;
using Xunit;

namespace learn_assist.Tests;

public class ImportDocumentViewModelTests
{
    [Fact]
    public void SelectUrl_ShowsUrlInputAndFiresEvent()
    {
        var vm = new ImportDocumentViewModel();
        bool urlRequested = false;
        WebsiteContentType? receivedType = null;
        vm.UrlImportRequested += t => { urlRequested = true; receivedType = t; };

        vm.SelectUrlCommand.Execute(null);

        Assert.Equal("URL", vm.SelectedTypeName);
        Assert.True(vm.IsUrlInputVisible);
        Assert.Null(vm.NewUrl);
        Assert.True(urlRequested);
        Assert.Equal(WebsiteContentType.Url, receivedType);
    }

    [Fact]
    public void SelectDocument_HidesUrlInput()
    {
        var vm = new ImportDocumentViewModel { IsUrlInputVisible = true };
        vm.SelectDocumentCommand.Execute(null);

        Assert.Equal("Document", vm.SelectedTypeName);
        Assert.False(vm.IsUrlInputVisible);
    }

    [Fact]
    public void SelectImage_HidesUrlInput()
    {
        var vm = new ImportDocumentViewModel { IsUrlInputVisible = true };
        vm.SelectImageCommand.Execute(null);

        Assert.Equal("Image", vm.SelectedTypeName);
        Assert.False(vm.IsUrlInputVisible);
    }

    [Fact]
    public void SelectVideo_HidesUrlInput()
    {
        var vm = new ImportDocumentViewModel { IsUrlInputVisible = true };
        vm.SelectVideoCommand.Execute(null);

        Assert.Equal("Video", vm.SelectedTypeName);
        Assert.False(vm.IsUrlInputVisible);
    }

    [Fact]
    public void NewUrl_CanBeSetAndCleared()
    {
        var vm = new ImportDocumentViewModel();
        vm.NewUrl = "https://ejemplo.com/pagina";
        Assert.Equal("https://ejemplo.com/pagina", vm.NewUrl);
        vm.NewUrl = null;
        Assert.Null(vm.NewUrl);
    }

    [Fact]
    public void ImportResult_StartsEmpty()
    {
        var vm = new ImportDocumentViewModel();
        Assert.Equal(string.Empty, vm.ImportResult);
    }

    [Fact]
    public void CancelCommand_CanBeExecuted()
    {
        var vm = new ImportDocumentViewModel();
        bool cancelled = false;
        vm.CancelRequested += () => cancelled = true;

        vm.CancelCommand.Execute(null);

        Assert.True(cancelled);
    }
}
