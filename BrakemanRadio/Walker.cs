using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrakemanRadio
{
	internal static class Walker
	{
		internal class ReachedJunction
		{
			public Junction junction;
			public bool isForward;
			public bool isMisaligned;
		}
		public static IEnumerable<ReachedJunction> WalkJunctions(RailTrack track, float direction)
		{
			var currentTrack = track;
			var currentDirection = direction * -1;
			Junction sourceJunction = null;
			bool outbound = false;
			while (currentTrack != null)
			{
				if (currentDirection < 0 && currentTrack.inJunction != null)
				{
					sourceJunction = currentTrack.inJunction;
					break;
				}
				if (currentDirection > 0 && currentTrack.outJunction != null)
				{
					sourceJunction = currentTrack.outJunction;
					break;
				}
				var nextTrack = GetNextTrack(currentTrack, currentDirection);
				currentTrack = nextTrack.Key;
				currentDirection = nextTrack.Value;
			}
			outbound = sourceJunction.inBranch?.track != currentTrack;
			return WalkJunctions(sourceJunction, outbound);
		}
		public static IEnumerable<ReachedJunction> WalkJunctions(Junction junction, bool outbound)
		{
			ReachedJunction lastJunction = new ReachedJunction();
			lastJunction.junction = junction;
			lastJunction.isForward = outbound;
			while (lastJunction?.junction != null)
			{
				lastJunction = GetNextJunction(lastJunction);
				if (lastJunction?.junction != null)
				{
					BrakemanRadioControl.Debug("Found junction " + lastJunction.junction.GetInstanceID() + "\t" + lastJunction.isForward);
					yield return lastJunction;
				}
			}
		}

		private static ReachedJunction GetNextJunction(ReachedJunction lastJunction)
		{
			var nextTrack = lastJunction.isForward ? lastJunction.junction.outBranches[lastJunction.junction.selectedBranch]?.track : lastJunction.junction.inBranch?.track;
			var nextJunction = new ReachedJunction();
			if (nextTrack.inJunction != null && nextTrack.outJunction != null)
			{
				// We have a one track span
				if (nextTrack.inJunction == lastJunction.junction) nextJunction.junction = nextTrack.outJunction;
				else nextJunction.junction = nextTrack.inJunction;
			} else
			{
				float nextDirection = nextTrack.inJunction == lastJunction.junction ? 1 : -1;
				do
				{
					var walkedTrack = GetNextTrack(nextTrack, nextDirection);
					nextTrack = walkedTrack.Key;
					nextDirection = walkedTrack.Value;
				} while (nextTrack != null && nextTrack.inJunction == null && nextTrack.outJunction == null);
				nextJunction.junction = nextDirection > 0 ? nextTrack.outJunction : nextTrack.inJunction;
			}
			nextJunction.isForward = nextJunction.junction?.inBranch?.track == nextTrack;
			nextJunction.isMisaligned = !nextJunction.isForward && nextJunction.junction?.outBranches[nextJunction.junction.selectedBranch]?.track != nextTrack;
			return nextJunction;
		}

		public static IEnumerable<KeyValuePair<RailTrack, float>> WalkTracks(RailTrack track, float direction)
		{
			BrakemanRadioControl.Debug("Walking tracks from " + track.logicTrack.ID.FullID + "\t" + direction);
			var currentTrack = track;
			var currentDireciton = direction;
			yield return new KeyValuePair<RailTrack, float>(currentTrack, direction);
			while (currentTrack != null)
			{
				var nextTrack = GetNextTrack(currentTrack, currentDireciton);
				if (nextTrack.Key != null)
				{
					BrakemanRadioControl.Debug("\t" + nextTrack.Key.logicTrack.ID.FullID + "\t" + nextTrack.Value);
					yield return nextTrack;
				}
				currentDireciton = nextTrack.Value;
				currentTrack = nextTrack.Key;
			}
		}

		private static KeyValuePair<RailTrack, float> GetNextTrack(RailTrack currentTrack, float direction)
		{
			RailTrack outTrack;
			if (direction < 0)
			{
				if (currentTrack.inJunction != null)
				{
					return GetNextTrack(currentTrack.inJunction, currentTrack);
				} else
				{
					outTrack = currentTrack.inBranch?.track;
				}
			} else
			{
				if (currentTrack.outJunction != null)
				{
					return GetNextTrack(currentTrack.outJunction, currentTrack);
				}
				else
				{
					outTrack = currentTrack.outBranch?.track;
				}
			}
			if (outTrack?.inBranch?.track == currentTrack)
			{
				return new KeyValuePair<RailTrack, float>(outTrack, 1);
			} else
			{
				return new KeyValuePair<RailTrack, float>(outTrack, -1);
			}
		}

		private static KeyValuePair<RailTrack, float> GetNextTrack(Junction inJunction, RailTrack currentTrack)
		{
			RailTrack outTrack;
			if (inJunction.inBranch.track == currentTrack)
			{
				outTrack = inJunction.outBranches[inJunction.selectedBranch].track;
			} else
			{
				outTrack = inJunction.inBranch.track;
			}
			if (outTrack.inJunction == inJunction)
			{
				return new KeyValuePair<RailTrack, float>(outTrack, 1);
			} else
			{
				return new KeyValuePair<RailTrack, float>(outTrack, -1);
			}
		}
	}
}
