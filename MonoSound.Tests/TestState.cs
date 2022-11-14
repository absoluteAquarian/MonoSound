using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoSound.Tests {
	internal class TestState {
		public readonly Keys key;
		public readonly string identifier;
		public readonly Action onSelected;

		public List<TestState> children;

		public TestState(Keys key, string identifier, Action onSelected) {
			this.key = key;
			this.identifier = identifier;
			this.onSelected = onSelected;
			children = new List<TestState>();
		}

		public TestState WithChild(TestState child) {
			children.Add(child);
			return this;
		}

		public TestState WithChildren(params TestState[] children) {
			this.children.AddRange(children);
			return this;
		}

		public TestState Get(Keys key) => children.FirstOrDefault(c => c.key == key);

		public IEnumerable<TestState> Get(IEnumerable<Keys> sequence) {
			if (!sequence.Any())
				return Array.Empty<TestState>();

			TestState state = this;

			List<TestState> tree = new List<TestState>();

			foreach (var key in sequence) {
				state = state.Get(key);

				if (state != null)
					tree.Add(state);
				else
					return Array.Empty<TestState>();
			}

			return tree;
		}
	}

	internal class TestStateMachine {
		private TestState tests;

		public void InitializeStates(params TestState[] tests) {
			this.tests = new TestState(Keys.None, "State", null).WithChildren(tests);
		}

		public IEnumerable<string> ReportCurrentTree(IEnumerable<Keys> sequence) {
			yield return "State:";
			
			StringBuilder indent = new StringBuilder("  ");

			IEnumerable<TestState> tree = tests.Get(sequence);

			TestState node = tests;
			foreach (TestState state in tree) {
				yield return indent.ToString() + state.identifier;
				node = state;
				indent.Append("  ");
			}

			if (node != tests)
				yield return $"{indent}[{Keys.Escape}]: Go Back";

			foreach (TestState child in node.children) {
				yield return $"{indent}[{child.key}]: {child.identifier}";
			}
		}

		public TestState GetCurrentNode(IEnumerable<Keys> sequence) => tests.Get(sequence).LastOrDefault();

		public void InvokeCurrent(IEnumerable<Keys> sequence) => GetCurrentNode(sequence)?.onSelected?.Invoke();
	}
}
