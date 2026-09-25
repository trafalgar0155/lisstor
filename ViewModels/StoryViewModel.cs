using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Net;
using lisstor.Models;
using lisstor.Services;

namespace lisstor.ViewModels;

public sealed class StoryViewModel : INotifyPropertyChanged
{
	private readonly StoryService _storyService;
	private StoryCard? _story;
	private WebViewSource? _readerSource;
	private string _errorMessage = string.Empty;
	private bool _isBusy;

	public StoryViewModel(StoryService storyService)
	{
		_storyService = storyService;
		RetryCommand = new Command(async () => await LoadAsync());
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public ICommand RetryCommand { get; }
	public string Title => _story?.Title ?? "Story";
	public string Author => _story?.Author ?? string.Empty;
	public string Metadata => _story?.Metadata ?? string.Empty;
	public WebViewSource? ReaderSource
	{
		get => _readerSource;
		private set => SetField(ref _readerSource, value);
	}

	public string ErrorMessage
	{
		get => _errorMessage;
		private set
		{
			if (SetField(ref _errorMessage, value))
				OnPropertyChanged(nameof(HasError));
		}
	}

	public bool IsBusy
	{
		get => _isBusy;
		private set => SetField(ref _isBusy, value);
	}

	public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

	public async Task LoadAsync(StoryCard? story = null)
	{
		if (story is not null)
		{
			_story = story;
			OnPropertyChanged(nameof(Title));
			OnPropertyChanged(nameof(Author));
			OnPropertyChanged(nameof(Metadata));
		}

		if (_story is null || IsBusy)
			return;

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			var content = await _storyService.GetStoryContentAsync(_story.Url);
			ReaderSource = new HtmlWebViewSource { Html = BuildReaderHtml(content) };
		}
		catch (Exception ex)
		{
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private string BuildReaderHtml(string content)
	{
		var title = WebUtility.HtmlEncode(Title);
		var author = WebUtility.HtmlEncode(Author);
		var metadata = WebUtility.HtmlEncode(Metadata);

		return $$"""
			<!doctype html>
			<html>
			<head>
			<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1" />
			<style>
			:root { color-scheme: light dark; }
			* { box-sizing: border-box; }
			html, body { margin: 0; padding: 0; background: #ffffff; color: #111318; }
			body { font-family: system-ui, -apple-system, sans-serif; }
			main { padding: 26px 24px 64px; }
			h1 { margin: 0; font-size: 34px; line-height: 1.12; letter-spacing: -0.5px; }
			.author { margin-top: 12px; color: #0b57d0; font-size: 15px; font-weight: 700; }
			.meta { margin-top: 5px; color: #5f6368; font-size: 14px; }
			hr { border: 0; border-top: 1px solid #e4e7ec; margin: 24px 0; }
			.story { font-size: 18px; line-height: 1.65; white-space: pre-wrap; overflow-wrap: anywhere; }
			.story p { margin: 0 0 1.15em; }
			.story p:last-child { margin-bottom: 0; }
			@media (prefers-color-scheme: dark) {
			  html, body { background: #0f1115; color: #e2e2e9; }
			  .author { color: #a8c7fa; }
			  .meta { color: #c4c6d0; }
			  hr { border-top-color: #303238; }
			}
			</style>
			</head>
			<body>
			<main>
			<h1>{{title}}</h1>
			<div class="author">{{author}}</div>
			<div class="meta">{{metadata}}</div>
			<hr />
			<article class="story">{{content}}</article>
			</main>
			</body>
			</html>
			""";
	}

	private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return false;
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
