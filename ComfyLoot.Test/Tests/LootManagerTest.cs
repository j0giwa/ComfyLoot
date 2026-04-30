
using NUnit.Framework;

using ComfyLoot.Managers;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;

namespace ComfyLoot.Test.Tests;

[TestFixture] 
public class LootManagerTest {

	[SetUp]
	public void Init()
	{
		Config.IsTestEnvironment = true;
	}

	// TODO: add tests
}