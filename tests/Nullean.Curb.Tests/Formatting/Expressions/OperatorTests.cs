namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>Binary, unary, conditional and assignment operators.</summary>
public class OperatorTests : FormattingTest
{
	[Test]
	public Task Arithmetic_operators_get_spaces() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first+second*third;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first + second * third;
		    }
		}
		""");

	[Test]
	public Task Comparison_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first < second && third >= fourth;
		    }
		}
		""");

	[Test]
	public Task Equality_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first == second || third != fourth;
		    }
		}
		""");

	[Test]
	public Task Bitwise_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first & second | third ^ fourth;
		    }
		}
		""");

	[Test]
	public Task Shift_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first << 2 >> 1;
		    }
		}
		""");

	[Test]
	public Task Coalesce_operator() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first ?? second;
		    }
		}
		""");

	[Test]
	public Task Coalesce_assignment() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        value ??= fallback;
		    }
		}
		""");

	// Issue #100: a broken &&/||/?? chain kept its breaks but lost its continuation indent, so every
	// operand after the first landed at the statement's own column. dotnet format leaves a
	// continuation line where the author put it, relative to the line above.
	[Test]
	public Task Broken_logical_chain_keeps_operands_aligned_under_the_first() => Unchanged(
		"""
		public class C
		{
		    public static bool Binary(string trimmed)
		    {
		        return trimmed.Length > 0
		               && !string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)
		               && !string.Equals(trimmed, "0", StringComparison.Ordinal);
		    }
		}
		""");

	[Test]
	public Task Broken_coalesce_keeps_its_aligned_continuation() => Unchanged(
		"""
		public class C
		{
		    public static string Coalesce(string? value)
		    {
		        var resolved = value?.Trim()
		                       ?? throw new InvalidOperationException("missing");
		        return resolved;
		    }
		}
		""");

	[Test]
	public Task Broken_logical_chain_keeps_a_one_level_continuation_indent() => Unchanged(
		"""
		public class C
		{
		    public bool M(string value)
		    {
		        var ok = value.Length > 0
		            || value.Length < 5;
		        return value.Length > 0
		            && value.Length < 5
		            && value[0] == 'a';
		    }
		}
		""");

	[Test]
	public Task Broken_logical_chain_with_the_operator_ending_the_line_keeps_its_indent() => Unchanged(
		"""
		public class C
		{
		    public bool M(string value)
		    {
		        return value.Length > 0 &&
		               value.Length < 5;
		    }
		}
		""");

	// Issue #101 reported the same lost indent after a property pattern.
	[Test]
	public Task Broken_logical_chain_after_a_pattern_keeps_its_indent() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version { Major: 1 } version
		            && version.Build > 0;
		    }
		}
		""");

	// The continuation moves with its statement when the statement is re-indented, keeping its
	// offset from the line above rather than its absolute column.
	[Test]
	public Task Broken_logical_chain_moves_with_a_reindented_statement() => Formats(
		"""
		public class C
		{
		    public bool M(string value)
		    {
		            return value.Length > 0
		                   && value.Length < 5;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(string value)
		    {
		        return value.Length > 0
		               && value.Length < 5;
		    }
		}
		""");

	// The offset is only meaningful while the first operand keeps its place on its line. Here Curb
	// moves it onto a line of its own, and an offset measured from the declaration's line pushed
	// the continuation 27 columns past it; it breaks to the ambient indent instead, under the first.
	[Test]
	public Task Broken_logical_chain_whose_first_operand_curb_moves_aligns_under_it() => Formats(
		"""
		public class C
		{
		    public bool M(int a, int b) => a > 0 &&
		                                   b > 0;
		}
		""",
		"""
		public class C
		{
		    public bool M(int a, int b) =>
		        a > 0 &&
		        b > 0;
		}
		""");

	[Test]
	public Task Broken_coalesce_in_a_lambda_curb_breaks_aligns_under_its_first_operand() => Formats(
		"""
		public class C
		{
		    public void M(string a, string b)
		    {
		        Use(x => x ??
		                 b);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(string a, string b)
		    {
		        Use(x =>
		                x ??
		                b);
		    }
		}
		""");

	// Expanding the accessor breaks before `return`, which sat mid-line. dotnet format leaves the
	// line after it at its source column rather than moving it by however far `return` moved.
	[Test]
	public Task Broken_logical_chain_in_an_expanded_block_keeps_its_source_column() => Formats(
		"""
		public class C
		{
		    public bool P
		    {
		        get { return A > 0 &&
		                     B > 0; }
		    }
		}
		""",
		"""
		public class C
		{
		    public bool P
		    {
		        get
		        {
		            return A > 0 &&
		                     B > 0;
		        }
		    }
		}
		""");

	// Issue #115: an own-line comment ahead of an operand, on either side of the operator, printed
	// at the statement's column and took the operand with it. dotnet format moves the operand as far
	// as the statement moved, and puts every comment above it at the operand's column.
	[Test]
	public Task Broken_logical_chain_keeps_its_indent_after_an_own_line_comment() => Formats(
		"""
		public class C
		{
		    public bool Before(bool a, bool b, bool c)
		    {
		            return a
		                // before the operator
		                && b
		                && c;
		    }

		    public bool After(bool a, bool b, bool c)
		    {
		            return a &&
		                // after the operator
		                b &&
		                c;
		    }

		    public bool Several(bool a, bool b)
		    {
		            return a
		                // one
		                /* two */
		                    // three
		                && b;
		    }

		    public bool Aligned(bool a, bool b, bool c)
		    {
		        return a
		               // aligned comment
		               && b
		                 // deeper comment
		               && c;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool Before(bool a, bool b, bool c)
		    {
		        return a
		            // before the operator
		            && b
		            && c;
		    }

		    public bool After(bool a, bool b, bool c)
		    {
		        return a &&
		            // after the operator
		            b &&
		            c;
		    }

		    public bool Several(bool a, bool b)
		    {
		        return a
		            // one
		            /* two */
		            // three
		            && b;
		    }

		    public bool Aligned(bool a, bool b, bool c)
		    {
		        return a
		               // aligned comment
		               && b
		               // deeper comment
		               && c;
		    }
		}
		""");

	// Conditional-compilation directives stay at column 0; the operand after one keeps its indent.
	[Test]
	public Task Broken_logical_chain_keeps_its_indent_after_a_directive() => Formats(
		"""
		public class C
		{
		    public bool M(bool a, bool b, bool c)
		    {
		            return a
		#if DEBUG
		                && b
		#else
		                && c
		#endif
		                && !c;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(bool a, bool b, bool c)
		    {
		        return a
		#if DEBUG
		                && b
		#else
		            && c
		#endif
		            && !c;
		    }
		}
		""");

	[Test]
	public Task Broken_logical_chain_keeps_its_indent_after_a_comment_under_tabs() => Formats(
		"public class C\n{\n\tpublic bool M(bool a, bool b)\n\t{\n\t\t\treturn a\n\t\t\t\t// why\n\t\t\t\t&& b;\n\t}\n}",
		"public class C\n{\n\tpublic bool M(bool a, bool b)\n\t{\n\t\treturn a\n\t\t\t// why\n\t\t\t&& b;\n\t}\n}",
		editorConfig: "indent_style = tab");

	// The right operand's own lines sit as far in from its continuation line as they would from the
	// indent. They fell back to the statement's column instead, four columns left of the `??` above.
	[Test]
	public Task Broken_chain_operand_keeps_its_own_lines_under_its_continuation() => Formats(
		"""
		public class C
		{
		    public Foo M(Foo a, bool b)
		    {
		        var q = a
		            ?? new Foo
		            {
		                X = 1
		            };
		        var r = a
		            ?? Make(
		                1);
		        var s = a ??
		            Make(
		                1,
		                2);
		        return b
		            || new Foo
		            {
		                X = 1
		            }.Ok;
		    }

		    public bool Lambda(Foo a, bool b)
		    {
		            return b
		                || Run(() =>
		                {
		                    Go();
		                });
		    }

		    public bool Nested(bool a, bool b, bool c)
		    {
		        return a
		            && (b
		                || Make(
		                    c));
		    }
		}
		""",
		"""
		public class C
		{
		    public Foo M(Foo a, bool b)
		    {
		        var q = a
		            ?? new Foo
		            {
		                X = 1
		            };
		        var r = a
		            ?? Make(
		                1);
		        var s = a ??
		            Make(
		                1,
		                2);
		        return b
		            || new Foo
		            {
		                X = 1
		            }.Ok;
		    }

		    public bool Lambda(Foo a, bool b)
		    {
		        return b
		            || Run(() =>
		            {
		                Go();
		            });
		    }

		    public bool Nested(bool a, bool b, bool c)
		    {
		        return a
		            && (b
		                || Make(
		                    c));
		    }
		}
		""");

	// Tabs as far as they reach, then spaces, on the operand's lines as on its continuation line.
	[Test]
	public Task Broken_chain_operand_keeps_its_own_lines_under_its_continuation_under_tabs() => Formats(
		"public class C\n{\n\tpublic Foo M(Foo a)\n\t{\n\t\t\tvar q = a\n\t\t\t      ?? new Foo\n\t\t\t      {\n\t\t\t          X = 1\n\t\t\t      };\n\t\treturn q;\n\t}\n}",
		"public class C\n{\n\tpublic Foo M(Foo a)\n\t{\n\t\tvar q = a\n\t\t\t  ?? new Foo\n\t\t\t  {\n\t\t\t\t  X = 1\n\t\t\t  };\n\t\treturn q;\n\t}\n}",
		editorConfig: "indent_style = tab");

	// A case label is not a statement: dotnet format moves a broken `when` chain's continuation lines
	// as far as the line `switch` starts on moved, not as far as the label moved.
	[Test]
	public Task Broken_chain_in_a_case_label_measures_from_the_switch() => Formats(
		"""
		public class C
		{
		    public bool LabelMoved(object o, bool a, bool b)
		    {
		        switch (o)
		        {
		                case Foo f when a &&
		                                b:
		                    return true;
		                case Foo g when a
		                    && b:
		                    return true;
		        }
		        return false;
		    }

		    public bool SwitchMoved(object o, bool a, bool b)
		    {
		            switch (o)
		            {
		                case Foo f when a &&
		                                b:
		                    return true;
		            }
		        return false;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool LabelMoved(object o, bool a, bool b)
		    {
		        switch (o)
		        {
		            case Foo f when a &&
		                                b:
		                return true;
		            case Foo g when a
		                    && b:
		                return true;
		        }
		        return false;
		    }

		    public bool SwitchMoved(object o, bool a, bool b)
		    {
		        switch (o)
		        {
		            case Foo f when a &&
		                            b:
		                return true;
		        }
		        return false;
		    }
		}
		""");

	[Test]
	public Task Is_and_as_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var first = value is string;
		        var second = value as string;
		    }
		}
		""");

	[Test]
	public Task Prefix_unary_operators_hug_their_operand() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = -number;
		        var negated = !flag;
		        var complement = ~bits;
		    }
		}
		""");

	[Test]
	public Task Increment_and_decrement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        counter++;
		        counter--;
		        ++counter;
		        --counter;
		    }
		}
		""");

	[Test]
	public Task Cast_has_no_space_after_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (int) number;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (int)number;
		    }
		}
		""");

	[Test]
	public Task Parenthesized_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (first + second) * third;
		    }
		}
		""");

	[Test]
	public Task Ternary_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = condition ? first : second;
		    }
		}
		""");

	[Test]
	public Task Nested_ternary() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first ? one : second ? two : three;
		    }
		}
		""");

	[Test]
	public Task Compound_assignments() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        value += 1;
		        value -= 1;
		        value *= 2;
		        value /= 2;
		        value %= 2;
		    }
		}
		""");

	[Test]
	public Task Throw_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = argument ?? throw new ArgumentNullException(nameof(argument));
		    }
		}
		""");

	[Test]
	public Task Lambda_arrow_gets_spaces() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(x=>x.Value);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(x => x.Value);
		    }
		}
		""");

	[Test]
	public Task Parenthesized_lambda() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call((first, second) => first + second);
		    }
		}
		""");

	[Test]
	public Task Lambda_with_no_parameters() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(() => Value);
		    }
		}
		""");

	[Test]
	public Task Lambda_with_a_block_body() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(x =>
		        {
		            return x.Value;
		        });
		    }
		}
		""");

	[Test]
	public Task Expression_bodied_lambda_wrapping_a_block_bodied_one() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Configure(builder => builder.Add(x =>
		        {
		            return x.Value;
		        }));
		    }
		}
		""");

	[Test]
	public Task Async_lambda() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(async x => await x.ValueAsync());
		    }
		}
		""");

	[Test]
	public Task Static_lambda() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(static x => x.Value);
		    }
		}
		""");

	[Test]
	public Task Typed_lambda_parameters() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call((int first, int second) => first + second);
		    }
		}
		""");

	[Test]
	public Task Discard_lambda_parameter() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call((_, _) => Value);
		    }
		}
		""");
}
