namespace lisstor.Models;

public sealed record StoryCard(
	string Title,
	string Author,
	string Description,
	string Metadata,
	string Url);
