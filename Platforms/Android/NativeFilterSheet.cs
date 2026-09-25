#if ANDROID
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Google.Android.Material.BottomSheet;
using Google.Android.Material.Button;
using Google.Android.Material.Chip;
using Google.Android.Material.TextField;
using lisstor.Models;
using lisstor.ViewModels;
using Microsoft.Maui.ApplicationModel;
using AColor = Android.Graphics.Color;

namespace lisstor;

internal static class NativeFilterSheet
{
	public static async Task ShowAsync(MainViewModel viewModel)
	{
		var activity = Platform.CurrentActivity;
		if (activity is null)
			return;

		await viewModel.PrepareFiltersAsync();

		var dialog = new BottomSheetDialog(activity);
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
		int Dp(int value) => (int)(value * density + 0.5f);
		var isDark = (activity.Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask) ==
			Android.Content.Res.UiMode.NightYes;
		var onSurface = isDark ? AColor.White : AColor.ParseColor("#1F1F1F");

		var root = new LinearLayout(activity)
		{
			Orientation = Orientation.Vertical,
			LayoutParameters = new ViewGroup.LayoutParams(
				ViewGroup.LayoutParams.MatchParent,
				(int)((activity.Resources?.DisplayMetrics?.HeightPixels ?? Dp(800)) * 0.88f))
		};
		root.SetPadding(Dp(20), Dp(10), Dp(20), Dp(16));

		var handle = new Android.Views.View(activity)
		{
			LayoutParameters = new LinearLayout.LayoutParams(Dp(32), Dp(4))
			{
				Gravity = GravityFlags.CenterHorizontal,
				BottomMargin = Dp(12)
			}
		};
		handle.Background = RoundedBackground(AColor.ParseColor("#C3C6CF"), Dp(2));
		root.AddView(handle);

		var header = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
		header.SetGravity(GravityFlags.CenterVertical);
		var title = new TextView(activity)
		{
			Text = "Filter stories",
			TextSize = 26,
			Typeface = Typeface.DefaultBold,
			LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
		};
		title.SetTextColor(onSurface);
		var close = new MaterialButton(activity)
		{
			Text = "✕",
			TextSize = 18,
			LayoutParameters = new LinearLayout.LayoutParams(Dp(48), Dp(48))
		};
		close.SetBackgroundColor(AColor.Transparent);
		close.SetTextColor(onSurface);
		close.Click += (_, _) => dialog.Dismiss();
		header.AddView(title);
		header.AddView(close);
		root.AddView(header);

		var content = new LinearLayout(activity) { Orientation = Orientation.Vertical };
		var scroll = new Android.Widget.ScrollView(activity)
		{
			FillViewport = true,
			LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f)
		};
		scroll.AddView(content);
		root.AddView(scroll);

		content.AddView(SectionLabel(activity, "Category", Dp));
		var categoryContainer = new TextInputLayout(new ContextThemeWrapper(
			activity,
			Resource.Style.Widget_Material3_TextInputLayout_OutlinedBox_ExposedDropdownMenu))
		{
			BoxBackgroundMode = TextInputLayout.BoxBackgroundOutline,
			Hint = "Story category",
			EndIconMode = TextInputLayout.EndIconDropdownMenu,
			LayoutParameters = SectionLayout(Dp)
		};
		var categoryDropdown = new MaterialAutoCompleteTextView(new ContextThemeWrapper(
			activity,
			Resource.Style.Widget_Material3_AutoCompleteTextView_OutlinedBox))
		{
			InputType = Android.Text.InputTypes.Null,
			DropDownHeight = Dp(320),
			LayoutParameters = new TextInputLayout.LayoutParams(
				ViewGroup.LayoutParams.MatchParent,
				Dp(56))
		};
		var categoryLabels = viewModel.Categories.Select(option => option.Label).ToArray();
		categoryDropdown.SetAdapter(new ArrayAdapter<string>(
			activity,
			Android.Resource.Layout.SimpleDropDownItem1Line,
			categoryLabels));
		categoryDropdown.SetText(viewModel.SelectedCategory.Label, false);
		categoryContainer.AddView(categoryDropdown);
		content.AddView(categoryContainer);

		content.AddView(SectionLabel(activity, "List type", Dp));
		var listTypeGroup = CreateToggleGroup(activity, viewModel.ListTypes, viewModel.SelectListTypeCommand, Dp);
		content.AddView(listTypeGroup);

		content.AddView(SectionLabel(activity, "Published", Dp));
		var daysGroup = CreateToggleGroup(activity, viewModel.LastDays, viewModel.SelectLastDaysCommand, Dp);
		content.AddView(daysGroup);

		content.AddView(SectionLabel(activity, "Tags", Dp));
		var chipGroup = new ChipGroup(activity)
		{
			SingleSelection = false,
			LayoutParameters = SectionLayout(Dp)
		};
		content.AddView(chipGroup);

		void PopulateTags()
		{
			chipGroup.RemoveAllViews();
			foreach (var option in viewModel.TagOptions)
			{
				var chip = new Chip(activity)
				{
					Text = option.Label,
					Checkable = true,
					Checked = option.IsSelected
				};
				chip.CheckedChange += (_, args) => option.IsSelected = args.IsChecked;
				chipGroup.AddView(chip);
			}
		}

		PopulateTags();
		categoryDropdown.ItemClick += async (_, args) =>
		{
			if (args.Position < 0 || args.Position >= viewModel.Categories.Count)
				return;

			categoryDropdown.Enabled = false;
			await viewModel.SelectCategoryAsync(viewModel.Categories[args.Position]);
			PopulateTags();
			categoryDropdown.Enabled = true;
		};

		var apply = new MaterialButton(activity)
		{
			Text = "Apply filters",
			TextSize = 16,
			LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(54))
			{
				TopMargin = Dp(12)
			}
		};
		apply.Click += async (_, _) =>
		{
			apply.Enabled = false;
			await viewModel.ApplyFiltersAsync();
			dialog.Dismiss();
		};
		root.AddView(apply);

		dialog.DismissEvent += (_, _) => completion.TrySetResult();
		dialog.SetContentView(root);
		dialog.Show();
		dialog.Behavior.State = BottomSheetBehavior.StateExpanded;
		dialog.Behavior.SkipCollapsed = true;
		await completion.Task;
	}

	private static MaterialButtonToggleGroup CreateToggleGroup(
		Activity activity,
		IReadOnlyList<SelectableFilterOption> options,
		System.Windows.Input.ICommand command,
		Func<int, int> dp)
	{
		var group = new MaterialButtonToggleGroup(activity)
		{
			SingleSelection = true,
			SelectionRequired = true,
			Orientation = Orientation.Horizontal,
			LayoutParameters = SectionLayout(dp)
		};

		foreach (var option in options)
		{
			var button = new MaterialButton(activity, null, Resource.Attribute.materialButtonOutlinedStyle)
			{
				Id = Android.Views.View.GenerateViewId(),
				Text = option.Label,
				TextSize = 13,
				Checkable = true,
				LayoutParameters = new LinearLayout.LayoutParams(0, dp(48), 1f)
			};
			button.Click += (_, _) => command.Execute(option);
			group.AddView(button);
			if (option.IsSelected)
				group.Check(button.Id);
		}

		return group;
	}

	private static TextView SectionLabel(Context context, string text, Func<int, int> dp) => new(context)
	{
		Text = text,
		TextSize = 14,
		Typeface = Typeface.DefaultBold,
		LayoutParameters = new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent,
			ViewGroup.LayoutParams.WrapContent)
		{
			TopMargin = dp(18),
			BottomMargin = dp(8)
		}
	};

	private static LinearLayout.LayoutParams SectionLayout(Func<int, int> dp) => new(
		ViewGroup.LayoutParams.MatchParent,
		ViewGroup.LayoutParams.WrapContent)
	{
		BottomMargin = dp(4)
	};

	private static GradientDrawable RoundedBackground(AColor color, int radius)
	{
		var drawable = new GradientDrawable();
		drawable.SetColor(color);
		drawable.SetCornerRadius(radius);
		return drawable;
	}
}
#endif


