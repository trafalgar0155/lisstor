namespace lisstor;

using lisstor.ViewModels;

public partial class MainPage : ContentPage
{
	private readonly MainViewModel _viewModel;

	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.InitializeAsync();
	}

	private async void OnFilterTapped(object? sender, TappedEventArgs e)
	{
#if ANDROID
		await NativeFilterSheet.ShowAsync(_viewModel);
#endif
	}
}
