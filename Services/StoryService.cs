using LiteroticaApi;
using LiteroticaApi.Api;
using lisstor.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace lisstor.Services;

public sealed class StoryService
{
	public async Task<string> GetStoryContentAsync(string storyUrl)
	{
		var slug = GetStorySlug(storyUrl);
		var firstPage = await Client.HttpClientInstance.GetFromJsonAsync<StoryContentResponse>(
			$"https://literotica.com/api/3/stories/{slug}?contentPage=1");

		if (firstPage is null)
			throw new InvalidOperationException("The story could not be loaded.");

		var pages = new List<string> { firstPage.PageText };
		for (var page = 2; page <= firstPage.Meta.PageCount; page++)
		{
			var nextPage = await Client.HttpClientInstance.GetFromJsonAsync<StoryContentResponse>(
				$"https://literotica.com/api/3/stories/{slug}?contentPage={page}");
			if (nextPage is not null)
				pages.Add(nextPage.PageText);
		}

		return string.Join("<br/><br/>", pages.Where(page => !string.IsNullOrWhiteSpace(page)));
	}

	private static string GetStorySlug(string storyUrl)
	{
		if (Uri.TryCreate(storyUrl, UriKind.Absolute, out var uri))
			return uri.AbsolutePath.Trim('/').Split('/').Last();

		return storyUrl.Trim('/').Split('/').Last();
	}

	private sealed record StoryContentResponse(
		[property: JsonPropertyName("meta")] StoryContentMeta Meta,
		[property: JsonPropertyName("pageText")] string PageText);

	private sealed record StoryContentMeta(
		[property: JsonPropertyName("pages_count")] int PageCount);

	public async Task<IReadOnlyList<StoryCard>> GetStoriesAsync(
		Types.Categories category,
		StoryFeed feed = StoryFeed.New,
		int lastDays = 0,
		IReadOnlyList<int>? tagIds = null,
		int page = 1)
	{
		var hasTags = tagIds is { Count: > 0 };
		if (feed == StoryFeed.Random && page == 1 && !hasTags)
			page = Random.Shared.Next(1, 16);

		IEnumerable<LiteroticaApi.DataObjects.Submission> stories;
		if (hasTags)
		{
			var tagged = await StoryApi.SearchForStoriesByTagsAsync(
				tagIds!.ToArray(),
				page,
				50,
				ToPeriod(lastDays),
				lastDays > 0);
			stories = tagged.Submissions.Where(story => story.Category == (int)category);
		}
		else
		{
			var response = await StoryApi.SearchForStoriesAsync(
				query: string.Empty,
				categories: [category],
				page: page,
				type: Types.WorkTypes.Story,
				languages: [Types.Languages.English]);
			stories = response.Data;
		}

		stories = stories.Where(story => !string.IsNullOrWhiteSpace(story.Title));
		if (lastDays > 0)
		{
			var cutoff = DateTime.UtcNow.Date.AddDays(-lastDays);
			stories = stories.Where(story =>
				DateTime.TryParse(story.DateApprove, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var approved) &&
				approved >= cutoff);
		}
		stories = feed switch
		{
			StoryFeed.Popular => stories
				.OrderByDescending(story => story.ViewCount ?? 0)
				.ThenByDescending(story => story.RateAll ?? 0),
			StoryFeed.Random => stories.OrderBy(_ => Random.Shared.Next()),
			_ => stories
		};

		return stories
			.Select(story => new StoryCard(
				story.Title,
				GetAuthor(story.Authorname.ToString()),
				string.IsNullOrWhiteSpace(story.Description)
					? "No description available."
					: story.Description.Trim(),
				BuildMetadata(story.RateAll, story.ReadingTime, story.ViewCount),
				NormalizeUrl(story.Url)))
			.ToArray();
	}

	public async Task<IReadOnlyList<SelectableFilterOption>> GetTopTagsAsync(Types.Categories category)
	{
		var response = await Client.HttpClientInstance.GetFromJsonAsync<List<LiteroticaApi.DataObjects.TagInfo>>(
			$"https://literotica.com/api/3/tagsportal/top?limit=24&periodCheck=false&category={(int)category}&period=all&language=1");
		if (response is null)
			return [];

		return response
			.Where(tag => (tag.Tagid ?? tag.Id) is not null && !string.IsNullOrWhiteSpace(tag.Tag))
			.Select(tag => new SelectableFilterOption(tag.Tag, (int)(tag.Tagid ?? tag.Id)!.Value))
			.ToArray();
	}

	private static Types.Period ToPeriod(int lastDays) => lastDays switch
	{
		7 => Types.Period.Week,
		30 => Types.Period.Month,
		_ => Types.Period.All
	};

	private static string NormalizeUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return string.Empty;
		if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
			return absoluteUri.ToString();

		return $"https://www.literotica.com/{url.TrimStart('/')}";
	}

	private static string GetAuthor(string? author) =>
		string.IsNullOrWhiteSpace(author) ? "Unknown author" : $"by {author}";

	private static string BuildMetadata(double? rating, long? readingTime, long? views)
	{
		var parts = new List<string>(3);

		if (rating is not null)
			parts.Add($"{rating:0.0} rating");
		if (readingTime is > 0)
			parts.Add($"{readingTime} min");
		if (views is > 0)
			parts.Add($"{FormatCount(views.Value)} views");

		return parts.Count == 0 ? "Story" : string.Join("  •  ", parts);
	}

	private static string FormatCount(long value) => value switch
	{
		>= 1_000_000 => $"{value / 1_000_000d:0.#}M",
		>= 1_000 => $"{value / 1_000d:0.#}K",
		_ => value.ToString()
	};
}
