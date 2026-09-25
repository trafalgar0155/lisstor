namespace lisstor;

public partial class AppShell : Shell
{
	public AppShell(MainPage mainPage)
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(StoryPage), typeof(StoryPage));
		MainContent.Content = mainPage;
	}
}
