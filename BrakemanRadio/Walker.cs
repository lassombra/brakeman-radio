using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrakemanRadio
{
	internal static class Walker
	{
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
