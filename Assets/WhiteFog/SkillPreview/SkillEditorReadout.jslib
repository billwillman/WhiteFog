mergeInto(LibraryManager.library, {
  SkillEditorReadout: function (json) {
    Module.canvas.dispatchEvent(new CustomEvent('skill-readout', {detail: JSON.parse(UTF8ToString(json))}));
  }
});
