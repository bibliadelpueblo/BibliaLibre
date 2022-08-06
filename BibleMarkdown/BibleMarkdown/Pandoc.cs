using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliWrap;

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
			var stdOutBuffer = new StringBuilder();
			var stdErrBuffer = new StringBuilder();

			var result = await Cli.Wrap(path)
				.WithArguments($@"""{sourcefile}"" -o ""{destfile}"" --from {sourceformat} --to {destformat}")
				.WithWorkingDirectory(Environment.CurrentDirectory)
				.WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
				.WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
				.ExecuteAsync();

			Program.Log(stdOutBuffer.ToString().Trim(' ', '\t', '\r', '\n'));
			Program.Log(stdErrBuffer.ToString().Trim(' ', '\t', '\r', '\n'));
		}
	}
}
