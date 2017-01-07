using System;
using System.IO;
using System.ComponentModel;
using System.Threading.Tasks;

namespace HybridKit.Apps {

	public interface IInvalidatable {
		/// <summary>
		/// Raised when the HTML content must be rewritten.
		/// </summary>
		event EventHandler Invalidated;
	}

	public interface IHtmlWriter : IInvalidatable {
		void WriteHtml (TextWriter writer);
	}

	public interface IBinding : IHtmlWriter, INotifyPropertyChanged {
		string Name { get; }
		object Value { get; set; }
	}

	public interface IUpdater {
		Task Update (IHtmlWriter writer);
	}
	public static class Updater {

		public static void AddUpdater (this IHtmlWriter obj, IUpdater updater)
		{
			obj.Invalidated += updater.OnUpdate;
		}
		public static void RemoveUpdater (this IHtmlWriter obj, IUpdater updater)
		{
			obj.Invalidated -= updater.OnUpdate;
		}
		static async void OnUpdate (this IUpdater updater, object sender, EventArgs e)
		{
			var writer = (IHtmlWriter)sender;
			await updater.Update (writer);
		}
	}
}
