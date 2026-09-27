This file is deliberately not a Godot resource pack. It sits at the .pck path a real mod would ship so that
ProjectSettings.LoadResourcePack returns false and Mod.LoadResources raises a ModLoadException, which is the
failure path this fixture exists to cover. A resource pack starts with a binary GDPC header; no amount of
text here will be mistaken for one.
