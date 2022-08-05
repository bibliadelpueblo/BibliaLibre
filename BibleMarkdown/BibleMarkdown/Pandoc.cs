using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BibleMarkdown
{
	public class Pandoc
	{
		static string path;
		public static void SetPandocPath(string exe) 
		{
			path = exe;
		}

		public static async Task RunAsync(string sourcefile, string destfile, string sourceformat, string destformat)
		{
			CliWrap.Command command = new CliWrap.Command(path)
				.WithArguments($@"""{sourcefile}"" ""{destfile}"" --from {sourceformat} --to {destformat}")
				.WithWorkingDirectory(Environment.CurrentDirectory);
			var token = new CancellationToken();
			await command.ExecuteAsync(token);
		}
	}
}
