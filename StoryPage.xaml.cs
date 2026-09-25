using lisstor.Models;
using lisstor.ViewModels;

namespace lisstor;

public partial class StoryPage : ContentPage, IQueryAttributable
{
	private readonly StoryViewModel _viewModel;

	public StoryPage(StoryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("Story", out var value) && value is StoryCard story)
			_ = _viewModel.LoadAsync(story);
	}

	private async void OnBackClicked(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync("..");
}
