using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MessageVault.Files;

namespace MessageVault.Api {

	public sealed class CacheReader : IDisposable{
		readonly IVolatileCheckpointVectorAccess _fastCheckpoint;
		public readonly FileCheckpointArrayReader SourceCheckpoint;
		readonly FileStream _sourceStream;
		readonly BinaryReader _reader;

		public static CacheReader CreateStandalone(string folder, string stream) {

			var streamFile = Path.Combine(folder, stream, CacheFetcher.CacheStreamName);
			var checkFile = Path.Combine(folder, stream, CacheFetcher.CachePositionName);


			var readOnce = new FileCheckpointArrayReader(new FileInfo(checkFile), CacheFetcher.CacheCheckpointSize);
			var vector = readOnce.Read();

			var fix = new FixedCheckpointArrayReader(vector);
			var cacheReader = new FileInfo(streamFile).Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			return new CacheReader(fix, cacheReader, readOnce);
		}

		public CacheReader(IVolatileCheckpointVectorAccess fastCheckpoint, FileStream sourceStream, FileCheckpointArrayReader sourceCheckpoint) {
			_fastCheckpoint = fastCheckpoint;
			_sourceStream = sourceStream;
			SourceCheckpoint = sourceCheckpoint;

			_reader = new BinaryReader(sourceStream, new UTF8Encoding(false));
		}

		public ReadBulkResult ReadAll(long startingFrom, int maxCount) {

			var result = new ReadBulkResult();
			var stats = ReadAll(startingFrom, maxCount, (id, position, maxPosition) => {
				if (result.Messages == null) {
					result.Messages = new List<MessageHandlerClosure>();
				}
				result.Messages.Add(new MessageHandlerClosure {
					CurrentCachePosition = position,
					MaxCachePosition = maxPosition,
					Message = id
				});
			});

			result.AvailableCachePosition = stats.AvailableCachePosition;
			result.CurrentCachePosition = stats.CurrentCachePosition;
			result.ReadEndOfCacheBeforeItWasFlushed = stats.ReadEndOfCacheBeforeItWasFlushed;
			result.ReadRecords = stats.ReadRecords;
			result.StartingCachePosition = stats.StartingCachePosition;
			result.MaxOriginPosition = stats.MaxOriginPosition;
			result.CachedOriginPosition = stats.CachedOriginPosition;
			return result;
		}

		public ReadResult ReadAll(long startingFrom, int maxCount, MessageHandler handler) {
			var longs = _fastCheckpoint.ReadPositionVolatile();
			var maxPos = longs[0];

			var result = new ReadResult() {
				StartingCachePosition = startingFrom,
				AvailableCachePosition = maxPos,
				CurrentCachePosition = startingFrom,
				CachedOriginPosition = longs[1],
				MaxOriginPosition = longs[2]
			};
			if (startingFrom >= maxPos) {
				return result;
			}


			// double check on the file
			maxPos = SourceCheckpoint.Read()[0];
			if (startingFrom >= maxPos) {
				return result;
			}
			
			_sourceStream.Seek(startingFrom, SeekOrigin.Begin);
			long currentPosition = startingFrom;
			try {
				for (int i = 0; i < maxCount; i++) {

					currentPosition = _sourceStream.Position;
					if (currentPosition >= maxPos) {
						break;
					}
					var frame = CacheStorage.Read(_reader);

					handler(frame, currentPosition, maxPos);
					// fix the position
					result.ReadRecords += 1;
					result.CurrentCachePosition = _sourceStream.Position;
				}
			}
			catch (InvalidStorageFormatException ex) {
				throw new InvalidStorageFormatException("Unexpected header at " + currentPosition, ex);
			}
			catch (NoDataException) {
				// not a problem, we just read end of cache before it was flushed to disk
				result.ReadEndOfCacheBeforeItWasFlushed = true;
			}
			catch (EndOfStreamException) {
				result.ReadEndOfCacheBeforeItWasFlushed = true;
			}

			return result;
		}


		public void Dispose() {
			_sourceStream.Dispose();
		}
	}

}