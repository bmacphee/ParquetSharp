﻿
//#define DUMP_EXPRESSION_TREES // uncomment in to get a dump on Console of the expression trees being created.

using System;
using ParquetSharp.IO;
using ParquetSharp.RowOriented;
using NUnit.Framework;

#if DUMP_EXPRESSION_TREES
using System.Linq.Expressions;
using System.Reflection;
#endif

namespace ParquetSharp.Test
{
    [TestFixture]
    internal static class TestRowOrientedParquetFile
    {
        [SetUp]
        public static void SetUp()
        {
#if DUMP_EXPRESSION_TREES
            ParquetFile.OnReadExpressionCreated = Dump;
            ParquetFile.OnWriteExpressionCreated = Dump;
#endif
        }

        [TearDown]
        public static void TearDown()
        {
            ParquetFile.OnReadExpressionCreated = null;
            ParquetFile.OnWriteExpressionCreated = null;
        }

        [Test]
        public static void TestRoundtrip()
        {
            TestRoundtrip(new[]
            {
                (123, 3.14f, new DateTime(1981, 06, 10)),
                (456, 1.27f, new DateTime(1987, 03, 16)),
                (789, 6.66f, new DateTime(2018, 05, 02))
            });

            TestRoundtrip(new[]
            {
                Tuple.Create(123, 3.14f, new DateTime(1981, 06, 10)),
                Tuple.Create(456, 1.27f, new DateTime(1987, 03, 16)),
                Tuple.Create(789, 6.66f, new DateTime(2018, 05, 02))
            });

            TestRoundtrip(new[]
            {
                new Row1 {A = 123, B = 3.14f, C = new DateTime(1981, 06, 10), D = 123.1M},
                new Row1 {A = 456, B = 1.27f, C = new DateTime(1987, 03, 16), D = 456.12M},
                new Row1 {A = 789, B = 6.66f, C = new DateTime(2018, 05, 02), D = 789.123M}
            });

            TestRoundtrip(new[]
            {
                new Row2 {A = 123, B = 3.14f, C = new DateTime(1981, 06, 10), D = 123.1M},
                new Row2 {A = 456, B = 1.27f, C = new DateTime(1987, 03, 16), D = 456.12M},
                new Row2 {A = 789, B = 6.66f, C = new DateTime(2018, 05, 02), D = 789.123M}
            });
        }

        [Test]
        public static void TestWriterDoubleDispose()
        {
            // ParquetRowWriter is not double-Dispose safe (Issue 64)
            // https://github.com/G-Research/ParquetSharp/issues/64

            using (var buffer = new ResizableBuffer())
            {
                using (var outputStream = new BufferOutputStream(buffer))
                using (var writer = ParquetFile.CreateRowWriter<(int, double, DateTime)>(outputStream))
                {
                    writer.Dispose();
                }
            }
        }

        [Test]
        public static void TestCompressionArgument([Values(Compression.Uncompressed, Compression.Brotli)] Compression compression)
        {
            using (var buffer = new ResizableBuffer())
            {
                using (var outputStream = new BufferOutputStream(buffer))
                using (var writer = ParquetFile.CreateRowWriter<(int, float)>(outputStream, compression: compression))
                {
                    writer.WriteRows(new[] {(42, 3.14f)});
                }

                using (var inputStream = new BufferReader(buffer))
                using (var reader = new ParquetFileReader(inputStream))
                using (var group = reader.RowGroup(0))
                {
                    Assert.AreEqual(2, group.MetaData.NumColumns);
                    Assert.AreEqual(compression, group.MetaData.GetColumnChunkMetaData(0).Compression);
                    Assert.AreEqual(compression, group.MetaData.GetColumnChunkMetaData(1).Compression);
                }
            }
        }

        private static void TestRoundtrip<TTuple>(TTuple[] rows)
        {
            using (var buffer = new ResizableBuffer())
            {
                using (var outputStream = new BufferOutputStream(buffer))
                using (var writer = ParquetFile.CreateRowWriter<TTuple>(outputStream))
                {
                    writer.WriteRows(rows);
                }

                using (var inputStream = new BufferReader(buffer))
                using (var reader = ParquetFile.CreateRowReader<TTuple>(inputStream))
                {
                    var values = reader.ReadRows(rowGroup: 0);

                    Assert.AreEqual(rows, values);
                }
            }
        }

        private sealed class Row1 : IEquatable<Row1>
        {
            public int A;
            public float B;
            public DateTime C;

            [ParquetDecimalScale(3)]
            public decimal D;

            public bool Equals(Row1 other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return A == other.A && B.Equals(other.B) && C.Equals(other.C) && D.Equals(other.D);
            }
        }

        private struct Row2
        {
            public int A { get; set; }
            public float B { get; set; }
            public DateTime C { get; set; }

            [ParquetDecimalScale(3)]
            public decimal D { get; set; }
        }

#if DUMP_EXPRESSION_TREES
        private static void Dump(Expression expression)
        {
            Console.WriteLine();
            Console.WriteLine(GetDebugView(expression));
            Console.WriteLine();
        }

        private static string GetDebugView(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }

            var propertyInfo = typeof(Expression).GetProperty("DebugView", BindingFlags.Instance | BindingFlags.NonPublic);
            if (propertyInfo == null)
            {
                throw new Exception("unable to reflect 'DebugView' property");
            }

            return propertyInfo.GetValue(expression) as string;
        }
#endif
    }
}
