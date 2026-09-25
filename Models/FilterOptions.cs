using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteroticaApi;

namespace lisstor.Models;

public sealed record CategoryFilterOption(string Label, Types.Categories Value);

public sealed class SelectableFilterOption(string label, int value) : INotifyPropertyChanged
{
	private bool _isSelected;

	public string Label { get; } = label;
	public int Value { get; } = value;

	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			if (_isSelected == value)
				return;
			_isSelected = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
}
