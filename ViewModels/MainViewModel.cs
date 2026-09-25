using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using LiteroticaApi;
using lisstor.Models;
using lisstor.Services;

namespace lisstor.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
	private const string CategoryPreference = "filters.category";
	private const string FeedPreference = "filters.feed";
	private const string DaysPreference = "filters.days";
	private const string TagsPreference = "filters.tags";

	private readonly StoryService _storyService;
	private bool _isBusy;
	private bool _isInitialized;
	private bool _isLoadingTags;
	private bool _isCategoryListOpen;
	private bool _suppressTagReload;
	private string _errorMessage = string.Empty;
	private int _loadVersion;
	private int _tagLoadVersion;
	private CategoryFilterOption _selectedCategory;
	private Types.Categories _appliedCategory;
	private StoryFeed _appliedFeed;
	private int _appliedDays;
	private HashSet<int> _appliedTagIds = [];

	public MainViewModel(StoryService storyService)
	{
		_storyService = storyService;
		Categories = Enum.GetValues<Types.Categories>()
			.Select(value => new CategoryFilterOption(Humanize(value.ToString()), value))
			.ToArray();
		_selectedCategory = Categories.First(option => option.Value == Types.Categories.NonErotic);

		ListTypes =
		[
			new("New", (int)StoryFeed.New),
			new("Popular", (int)StoryFeed.Popular),
			new("Random", (int)StoryFeed.Random)
		];
		LastDays =
		[
			new("Last 7 days", 7),
			new("Last 30 days", 30),
			new("All time", 0)
		];

		RefreshCommand = new Command(async () => await LoadStoriesAsync());
		RetryCommand = new Command(async () => await LoadStoriesAsync());
		OpenStoryCommand = new Command<StoryCard>(async story => await OpenStoryAsync(story));
		SelectListTypeCommand = new Command<SelectableFilterOption>(option => SelectSingle(ListTypes, option));
		SelectLastDaysCommand = new Command<SelectableFilterOption>(option => SelectSingle(LastDays, option));
		ToggleTagCommand = new Command<SelectableFilterOption>(option => option.IsSelected = !option.IsSelected);
		ToggleCategoryListCommand = new Command(() => IsCategoryListOpen = !IsCategoryListOpen);
		SelectCategoryCommand = new Command<CategoryFilterOption>(option =>
		{
			SelectedCategory = option;
			IsCategoryListOpen = false;
		});
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public ObservableCollection<StoryCard> Stories { get; } = [];
	public ObservableCollection<SelectableFilterOption> TagOptions { get; } = [];
	public IReadOnlyList<CategoryFilterOption> Categories { get; }
	public IReadOnlyList<SelectableFilterOption> ListTypes { get; }
	public IReadOnlyList<SelectableFilterOption> LastDays { get; }
	public ICommand RefreshCommand { get; }
	public ICommand RetryCommand { get; }
	public ICommand OpenStoryCommand { get; }
	public ICommand SelectListTypeCommand { get; }
	public ICommand SelectLastDaysCommand { get; }
	public ICommand ToggleTagCommand { get; }
	public ICommand ToggleCategoryListCommand { get; }
	public ICommand SelectCategoryCommand { get; }

	public bool IsCategoryListOpen
	{
		get => _isCategoryListOpen;
		set
		{
			if (_isCategoryListOpen == value)
				return;
			_isCategoryListOpen = value;
			OnPropertyChanged();
		}
	}

	public CategoryFilterOption SelectedCategory
	{
		get => _selectedCategory;
		set
		{
			if (value is null || _selectedCategory == value)
				return;
			_selectedCategory = value;
			OnPropertyChanged();
			if (!_suppressTagReload)
				_ = LoadTagsAsync(value.Value, new HashSet<int>());
		}
	}

	public bool IsBusy
	{
		get => _isBusy;
		private set
		{
			if (_isBusy == value)
				return;
			_isBusy = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsEmpty));
		}
	}

	public bool IsLoadingTags
	{
		get => _isLoadingTags;
		private set
		{
			if (_isLoadingTags == value)
				return;
			_isLoadingTags = value;
			OnPropertyChanged();
		}
	}

	public string ErrorMessage
	{
		get => _errorMessage;
		private set
		{
			if (_errorMessage == value)
				return;
			_errorMessage = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(HasError));
			OnPropertyChanged(nameof(IsEmpty));
		}
	}

	public string FilterSummary => $"{Humanize(_appliedCategory.ToString())}  ·  {Humanize(_appliedFeed.ToString())}";
	public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
	public bool IsEmpty => !IsBusy && !HasError && Stories.Count == 0;

	public async Task InitializeAsync()
	{
		if (_isInitialized)
			return;

		_isInitialized = true;
		LoadPreferences();
		await LoadStoriesAsync();
	}

	public async Task PrepareFiltersAsync()
	{
		IsCategoryListOpen = false;
		_suppressTagReload = true;
		SelectedCategory = Categories.FirstOrDefault(option => option.Value == _appliedCategory)
			?? Categories.First(option => option.Value == Types.Categories.NonErotic);
		_suppressTagReload = false;

		SelectSingle(ListTypes, ListTypes.First(option => option.Value == (int)_appliedFeed));
		SelectSingle(LastDays, LastDays.First(option => option.Value == _appliedDays));
		await LoadTagsAsync(SelectedCategory.Value, _appliedTagIds);
	}

	public async Task ApplyFiltersAsync()
	{
		_appliedCategory = SelectedCategory.Value;
		_appliedFeed = (StoryFeed)ListTypes.First(option => option.IsSelected).Value;
		_appliedDays = LastDays.First(option => option.IsSelected).Value;
		_appliedTagIds = TagOptions.Where(option => option.IsSelected).Select(option => option.Value).ToHashSet();

		Preferences.Default.Set(CategoryPreference, (int)_appliedCategory);
		Preferences.Default.Set(FeedPreference, (int)_appliedFeed);
		Preferences.Default.Set(DaysPreference, _appliedDays);
		Preferences.Default.Set(TagsPreference, string.Join(',', _appliedTagIds));
		OnPropertyChanged(nameof(FilterSummary));
		await LoadStoriesAsync();
	}

	public async Task SelectCategoryAsync(CategoryFilterOption option)
	{
		_suppressTagReload = true;
		SelectedCategory = option;
		_suppressTagReload = false;
		await LoadTagsAsync(option.Value, new HashSet<int>());
	}

	private void LoadPreferences()
	{
		_appliedCategory = (Types.Categories)Preferences.Default.Get(CategoryPreference, (int)Types.Categories.NonErotic);
		_appliedFeed = (StoryFeed)Preferences.Default.Get(FeedPreference, (int)StoryFeed.New);
		_appliedDays = Preferences.Default.Get(DaysPreference, 0);
		_appliedTagIds = Preferences.Default.Get(TagsPreference, string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(value => int.TryParse(value, out var id) ? id : 0)
			.Where(id => id > 0)
			.ToHashSet();
		OnPropertyChanged(nameof(FilterSummary));
	}

	private async Task LoadTagsAsync(Types.Categories category, IReadOnlySet<int> selectedIds)
	{
		var version = ++_tagLoadVersion;
		IsLoadingTags = true;
		try
		{
			var tags = await _storyService.GetTopTagsAsync(category);
			if (version != _tagLoadVersion)
				return;

			TagOptions.Clear();
			foreach (var tag in tags)
			{
				tag.IsSelected = selectedIds.Contains(tag.Value);
				TagOptions.Add(tag);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Tag loading failed: {ex}");
#if ANDROID
			Android.Util.Log.Error("lisstor-tags", ex.ToString());
#endif
			if (version == _tagLoadVersion)
				TagOptions.Clear();
		}
		finally
		{
			if (version == _tagLoadVersion)
				IsLoadingTags = false;
		}
	}

	private async Task LoadStoriesAsync()
	{
		var version = ++_loadVersion;
		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			var stories = await _storyService.GetStoriesAsync(
				_appliedCategory,
				_appliedFeed,
				_appliedDays,
				_appliedTagIds.ToArray());
			if (version != _loadVersion)
				return;

			Stories.Clear();
			foreach (var story in stories)
				Stories.Add(story);
			OnPropertyChanged(nameof(IsEmpty));
		}
		catch (Exception ex)
		{
			if (version == _loadVersion)
				ErrorMessage = ex.Message;
		}
		finally
		{
			if (version == _loadVersion)
				IsBusy = false;
		}
	}

	private static void SelectSingle(IEnumerable<SelectableFilterOption> options, SelectableFilterOption? selected)
	{
		if (selected is null)
			return;
		foreach (var option in options)
			option.IsSelected = ReferenceEquals(option, selected);
	}

	private static async Task OpenStoryAsync(StoryCard? story)
	{
		if (story is null)
			return;

		await Shell.Current.GoToAsync(nameof(StoryPage), new Dictionary<string, object> { ["Story"] = story });
	}

	private static string Humanize(string value) =>
		Regex.Replace(value, "(?<!^)([A-Z])", " $1").Replace("and", " and ").Replace("  ", " ");

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
