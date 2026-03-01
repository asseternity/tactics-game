@tool
extends Control

# 引入翻译管理器
const TranslationManager = preload("res://addons/json_editor/scripts/translation_manager.gd")

enum ViewMode {
	TREE_VIEW,
	TABLE_VIEW,
	SPLIT_VIEW
}

enum ValueType {
	STRING,
	NUMBER,
	BOOLEAN,
	DICTIONARY,
	ARRAY
}

# Node references
@onready var file_path_edit: LineEdit = %FilePathEdit
@onready var json_edit: TextEdit = %JsonEdit
@onready var status_label: Label = %StatusLabel
@onready var file_dialog: FileDialog = %FileDialog
@onready var chinese_button: Button = %ChineseButton
@onready var english_button: Button = %EnglishButton
@onready var json_tree: Tree = %JsonTree
@onready var json_table: Control = %JsonTable
@onready var view_tabs: TabContainer = %ViewTabs
@onready var edit_dialog: Window = %EditDialog
@onready var edit_key: LineEdit = %EditKey
@onready var edit_value: LineEdit = %EditValue
@onready var add_dialog: Window = %AddDialog
@onready var add_key: LineEdit = %AddKey
@onready var add_value: LineEdit = %AddValue
@onready var type_option: OptionButton = %TypeOption
@onready var edit_type_option: OptionButton = %EditTypeOption

# Data management
var current_file_path: String = ""
var current_data: Variant
var current_item: TreeItem
var current_language: String = "zh"  # 默认中文

# 实时更新节流机制
var realtime_update_timer: Timer = null
var pending_realtime_update: bool = false
var realtime_update_delay: float = 0.1  # 100ms延迟，平衡响应性和性能

func _ready() -> void:
	# Disconnect all possible old connections
	_disconnect_all_signals()

	# Reconnect all signals
	_connect_all_signals()

	# Initialize interface
	_initialize_interface()

	# 初始化实时更新定时器
	_setup_realtime_update_timer()

func _disconnect_all_signals() -> void:
	# Button signals
	if %LoadButton.pressed.is_connected(_on_load_pressed):
		%LoadButton.pressed.disconnect(_on_load_pressed)
	if %SaveButton.pressed.is_connected(_on_save_pressed):
		%SaveButton.pressed.disconnect(_on_save_pressed)
	if %BrowseButton.pressed.is_connected(_on_browse_pressed):
		%BrowseButton.pressed.disconnect(_on_browse_pressed)
	if %AddRowButton.pressed.is_connected(_on_add_row_pressed):
		%AddRowButton.pressed.disconnect(_on_add_row_pressed)
	if chinese_button.pressed.is_connected(_on_chinese_button_pressed):
		chinese_button.pressed.disconnect(_on_chinese_button_pressed)
	if english_button.pressed.is_connected(_on_english_button_pressed):
		english_button.pressed.disconnect(_on_english_button_pressed)

	# File dialog signals
	if file_dialog.file_selected.is_connected(_on_file_selected):
		file_dialog.file_selected.disconnect(_on_file_selected)

	# Tree view signals
	if json_tree.item_activated.is_connected(_on_tree_item_activated):
		json_tree.item_activated.disconnect(_on_tree_item_activated)

	# Edit dialog signals
	if %EditConfirm.pressed.is_connected(_on_edit_confirm):
		%EditConfirm.pressed.disconnect(_on_edit_confirm)
	if %EditCancel.pressed.is_connected(_on_edit_cancel):
		%EditCancel.pressed.disconnect(_on_edit_cancel)
	if %DeleteButton.pressed.is_connected(_on_delete_pressed):
		%DeleteButton.pressed.disconnect(_on_delete_pressed)
	if %AddNewButton.pressed.is_connected(_on_add_new_pressed):
		%AddNewButton.pressed.disconnect(_on_add_new_pressed)
	if edit_dialog.close_requested.is_connected(_on_edit_cancel):
		edit_dialog.close_requested.disconnect(_on_edit_cancel)

	# Add dialog signals
	if %AddConfirm.pressed.is_connected(_on_add_confirm):
		%AddConfirm.pressed.disconnect(_on_add_confirm)
	if %AddCancel.pressed.is_connected(_on_add_cancel):
		%AddCancel.pressed.disconnect(_on_add_cancel)
	if add_dialog.close_requested.is_connected(_on_add_cancel):
		add_dialog.close_requested.disconnect(_on_add_cancel)

	# Type selection signals
	if type_option.item_selected.is_connected(_on_type_selected):
		type_option.item_selected.disconnect(_on_type_selected)

func _connect_all_signals() -> void:
	# Button signals
	%LoadButton.pressed.connect(_on_load_pressed)
	%SaveButton.pressed.connect(_on_save_pressed)
	%BrowseButton.pressed.connect(_on_browse_pressed)
	%AddRowButton.pressed.connect(_on_add_row_pressed)
	chinese_button.pressed.connect(_on_chinese_button_pressed)
	english_button.pressed.connect(_on_english_button_pressed)
	file_dialog.file_selected.connect(_on_file_selected)

	# Tree view signals
	json_tree.item_activated.connect(_on_tree_item_activated)

	# Edit dialog signals
	%EditConfirm.pressed.connect(_on_edit_confirm)
	%EditCancel.pressed.connect(_on_edit_cancel)
	%DeleteButton.pressed.connect(_on_delete_pressed)
	%AddNewButton.pressed.connect(_on_add_new_pressed)
	edit_dialog.close_requested.connect(_on_edit_cancel)

	# Add dialog signals
	%AddConfirm.pressed.connect(_on_add_confirm)
	%AddCancel.pressed.connect(_on_add_cancel)
	add_dialog.close_requested.connect(_on_add_cancel)

	# Type selection signals
	type_option.item_selected.connect(_on_type_selected)

func update_translations() -> void:
	"""更新UI翻译"""
	# 更新标签页标题
	if view_tabs:
		view_tabs.set_tab_title(0, TranslationManager.get_text("tree_view"))
		view_tabs.set_tab_title(1, TranslationManager.get_text("table_view"))

	# 更新UI标签文本
	var tree_label = get_node("MarginContainer/VBoxContainer/HSplitContainer/ViewTabs/TreeView/VBoxContainer/Label")
	if tree_label:
		tree_label.text = TranslationManager.get_text("tree_view_with_edit")

	var table_label = get_node("MarginContainer/VBoxContainer/HSplitContainer/ViewTabs/TableView/VBoxContainer/HBoxContainer/Label")
	if table_label:
		table_label.text = TranslationManager.get_text("excel_style_table_view")

	var add_row_button = %AddRowButton
	if add_row_button:
		add_row_button.text = TranslationManager.get_text("add_row")

	# 更新树形视图列标题
	if json_tree:
		json_tree.set_column_title(0, TranslationManager.get_text("key"))
		json_tree.set_column_title(1, TranslationManager.get_text("value"))

	# 更新头部按钮文本
	var load_button = %LoadButton
	if load_button:
		load_button.text = TranslationManager.get_text("load")

	var save_button = %SaveButton
	if save_button:
		save_button.text = TranslationManager.get_text("save")

	var browse_button = %BrowseButton
	if browse_button:
		browse_button.text = TranslationManager.get_text("browse")

	# 更新其他UI标签
	var file_path_label = get_node("MarginContainer/VBoxContainer/HeaderBar/Label")
	if file_path_label:
		file_path_label.text = TranslationManager.get_text("file_path") + ":"

	var language_label = get_node("MarginContainer/VBoxContainer/HeaderBar/LanguageLabel")
	if language_label:
		language_label.text = TranslationManager.get_text("language") + ":"

	var editor_label = get_node("MarginContainer/VBoxContainer/HSplitContainer/EditorPanel/VBoxContainer/Label")
	if editor_label:
		editor_label.text = TranslationManager.get_text("json_text") + ":"

	# 更新文件路径输入框占位符
	var file_path_edit = %FilePathEdit
	if file_path_edit:
		file_path_edit.placeholder_text = TranslationManager.get_text("enter_json_file_path")

func _initialize_interface() -> void:
	# Set tree column titles
	json_tree.set_column_title(0, TranslationManager.get_text("key"))
	json_tree.set_column_title(1, TranslationManager.get_text("value"))
	json_tree.set_column_expand(0, true)
	json_tree.set_column_expand(1, true)
	json_tree.set_column_custom_minimum_width(0, 150)
	json_tree.set_column_custom_minimum_width(1, 150)

	# Set file dialog
	file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	file_dialog.access = FileDialog.ACCESS_RESOURCES
	file_dialog.filters = PackedStringArray(["*.json ; JSON Files"])

	# Initialize type selection dropdown
	type_option.clear()
	type_option.add_item("String", ValueType.STRING)
	type_option.add_item("Number", ValueType.NUMBER)
	type_option.add_item("Boolean", ValueType.BOOLEAN)
	type_option.add_item("Dictionary", ValueType.DICTIONARY)
	type_option.add_item("Array", ValueType.ARRAY)

	# Initialize edit dialog type selection
	edit_type_option.clear()
	edit_type_option.add_item("String", ValueType.STRING)
	edit_type_option.add_item("Number", ValueType.NUMBER)
	edit_type_option.add_item("Boolean", ValueType.BOOLEAN)

	# Set window parent
	edit_dialog.gui_embed_subwindows = false
	add_dialog.gui_embed_subwindows = false

	# 设置Tab标签的多语言名称
	if view_tabs:
		view_tabs.set_tab_title(0, TranslationManager.get_text("tree_view"))
		view_tabs.set_tab_title(1, TranslationManager.get_text("table_view"))

	# 初始化语言按钮状态
	_update_language_button_states()

func _update_tree_view(data: Variant) -> void:
	json_tree.clear()
	var root = json_tree.create_item()
	root.set_text(0, "Root")
	root.set_metadata(0, {
		"is_container": true,
		"container_data": data,
		"parent_data": null,
		"key": null
	})
	_add_json_to_tree(data, root)

func _update_table_view(data: Variant) -> void:
	# 确保Excel表格组件已加载
	if not json_table.has_method("setup_data"):
		_setup_excel_table()

	if json_table and json_table.has_method("setup_data"):
		await json_table.setup_data(data)

		# 连接表格数据变化信号
		if not json_table.is_connected("data_changed", _on_table_data_changed):
			json_table.data_changed.connect(_on_table_data_changed)

		# 连接列类型编辑信号
		if not json_table.is_connected("column_type_edit_requested", _on_column_type_edit_requested):
			json_table.column_type_edit_requested.connect(_on_column_type_edit_requested)

func _setup_excel_table():
	# 动态加载和设置Excel表格脚本
	var excel_script = load("res://addons/json_editor/scripts/excel_table.gd")
	if excel_script and json_table:
		json_table.set_script(excel_script)

func _setup_realtime_update_timer():
	"""设置实时更新定时器，优化性能"""
	realtime_update_timer = Timer.new()
	realtime_update_timer.wait_time = realtime_update_delay
	realtime_update_timer.one_shot = true
	realtime_update_timer.timeout.connect(_perform_realtime_update)
	add_child(realtime_update_timer)
	print("实时更新定时器已初始化，延迟:", realtime_update_delay, "秒")

func _perform_realtime_update():
	"""执行实时更新，避免频繁更新"""
	if pending_realtime_update:
		print("执行实时更新")
		json_edit.text = JSON.stringify(current_data, "\t")
		# 同步更新树形视图
		_update_tree_view(current_data)
		pending_realtime_update = false
		print("实时更新完成")

func _request_realtime_update():
	"""请求实时更新（使用节流机制）"""
	if not pending_realtime_update:
		pending_realtime_update = true
		print("实时更新已请求")

	# 重启定时器（如果有新的更新，会重新计时）
	if realtime_update_timer:
		realtime_update_timer.stop()
		realtime_update_timer.start()

func _on_table_data_changed(new_data: Variant) -> void:
	current_data = new_data

	# 使用节流机制进行实时更新
	_request_realtime_update()

	# 显示数据已修改的提示
	_show_status(TranslationManager.get_text("data_modified_save_reminder"), false)

func _on_add_row_pressed() -> void:
	# 调用表格组件的添加行方法（在最后添加新行）
	if json_table and json_table.has_method("_add_row"):
		var row_count = 0
		if json_table.has_method("get_row_count"):
			row_count = json_table.get_row_count()
		elif json_table.rows_data:
			row_count = json_table.rows_data.size()

		json_table._add_row(row_count)  # 在末尾添加新行
		_show_status(TranslationManager.get_text("add_row") + " - " + TranslationManager.get_text("data_modified_save_reminder"), false)

func _on_column_type_edit_requested(column_index: int, column_name: String) -> void:
	# 显示列类型编辑对话框
	_show_column_type_dialog(column_index, column_name)

func _show_column_type_dialog(column_index: int, column_name: String) -> void:
	# 创建列编辑对话框（包含类型和名称编辑）
	var dialog = AcceptDialog.new()
	dialog.title = TranslationManager.get_text("edit_column") + ": " + column_name
	dialog.size = Vector2(450, 380)

	# 设置按钮文本
	dialog.ok_button_text = TranslationManager.get_text("confirm")

	var vbox = VBoxContainer.new()
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	vbox.add_theme_constant_override("margin_left", 10)
	vbox.add_theme_constant_override("margin_top", 10)
	vbox.add_theme_constant_override("margin_right", 10)
	vbox.add_theme_constant_override("margin_bottom", 10)
	dialog.add_child(vbox)

	var info_label = Label.new()
	var current_type_name = TranslationManager.get_text("unknown")
	if json_table and json_table.has_method("_get_type_icon"):
		var type_icon = json_table._get_type_icon(column_index)
		current_type_name = _get_type_name_from_icon(type_icon)

	info_label.text = TranslationManager.get_text("column_index") + ": " + str(column_index) + "\n" + TranslationManager.get_text("current_type") + ": " + current_type_name
	info_label.add_theme_font_size_override("font_size", 14)
	vbox.add_child(info_label)

	var separator1 = HSeparator.new()
	vbox.add_child(separator1)

	# 列名编辑部分
	var name_label = Label.new()
	name_label.text = TranslationManager.get_text("column_name") + ":"
	vbox.add_child(name_label)

	var name_edit = LineEdit.new()
	name_edit.text = column_name
	name_edit.placeholder_text = TranslationManager.get_text("enter_new_column_name")

	# 检查是否可以编辑列名
	var can_edit_name = false
	if json_table and json_table.has_method("can_edit_column_name"):
		can_edit_name = json_table.can_edit_column_name(column_index)

	if not can_edit_name:
		name_edit.editable = false
		name_edit.tooltip_text = TranslationManager.get_text("cannot_modify_column_name")

	vbox.add_child(name_edit)

	var separator2 = HSeparator.new()
	vbox.add_child(separator2)

	var type_label = Label.new()
	type_label.text = TranslationManager.get_text("select_data_type_conversion") + ":"
	vbox.add_child(type_label)

	var type_option = OptionButton.new()
	type_option.add_item(TranslationManager.get_text("string_type_full"), 0)
	type_option.add_item(TranslationManager.get_text("number_type_full"), 1)
	type_option.add_item(TranslationManager.get_text("boolean_type_full"), 2)

	# 获取当前列的类型并设置为默认选择
	var current_type = 0
	if json_table and json_table.has_method("get_column_type"):
		current_type = json_table.get_column_type(column_index)
	type_option.select(current_type)

	vbox.add_child(type_option)

	# 预览转换结果
	var preview_label = Label.new()
	preview_label.text = TranslationManager.get_text("conversion_preview") + ":"
	vbox.add_child(preview_label)

	var preview_text = TextEdit.new()
	preview_text.custom_minimum_size = Vector2(350, 100)
	preview_text.editable = false
	preview_text.placeholder_text = TranslationManager.get_text("select_type_to_preview")
	vbox.add_child(preview_text)

	# 类型选择变化时更新预览
	type_option.item_selected.connect(_on_type_preview_changed.bind(column_index, type_option, preview_text))

	var hint_label = Label.new()
	hint_label.text = TranslationManager.get_text("type_conversion_note")
	hint_label.add_theme_color_override("font_color", Color.ORANGE)
	hint_label.add_theme_font_size_override("font_size", 12)
	vbox.add_child(hint_label)

	var button_container = HBoxContainer.new()
	vbox.add_child(button_container)

	# 应用按钮 - 同时处理名称和类型编辑
	var apply_button = Button.new()
	apply_button.text = TranslationManager.get_text("apply_changes")
	apply_button.pressed.connect(_on_apply_column_changes.bind(column_index, name_edit, type_option, dialog, can_edit_name, false))
	button_container.add_child(apply_button)

	var force_convert_button = Button.new()
	force_convert_button.text = TranslationManager.get_text("force_convert")
	force_convert_button.add_theme_color_override("font_color", Color.RED)
	force_convert_button.pressed.connect(_on_apply_column_changes.bind(column_index, name_edit, type_option, dialog, can_edit_name, true))
	button_container.add_child(force_convert_button)

	var cancel_button = Button.new()
	cancel_button.text = TranslationManager.get_text("cancel")
	cancel_button.pressed.connect(dialog.queue_free)
	button_container.add_child(cancel_button)

	# 初始预览
	_on_type_preview_changed(column_index, type_option, preview_text, current_type)

	add_child(dialog)
	dialog.popup_centered()

func _on_convert_column_type(column_index: int, type_option: OptionButton, dialog: AcceptDialog, force_convert: bool = false) -> void:
	var selected_type = type_option.get_selected_id()
	await _convert_table_column_type(column_index, selected_type, force_convert)
	dialog.queue_free()
	_show_status(TranslationManager.get_text("column_type_conversion_complete"), false)

func _on_apply_column_changes(column_index: int, name_edit: LineEdit, type_option: OptionButton, dialog: AcceptDialog, can_edit_name: bool, force_convert: bool = false) -> void:
	var changes_made = false
	var error_messages = []

	# 处理列名更改
	if can_edit_name:
		var new_name = name_edit.text.strip_edges()
		var current_name = ""
		if json_table and json_table.has_method("get_column_name"):
			current_name = json_table.get_column_name(column_index)
		else:
			# 从headers获取当前名称
			if json_table and json_table.headers and column_index < json_table.headers.size():
				current_name = json_table.headers[column_index]

		if new_name != current_name:
			if new_name.is_empty():
				error_messages.append("列名称不能为空")
			else:
				# 尝试重命名列
				if json_table and json_table.has_method("rename_column"):
					var rename_success = await json_table.rename_column(column_index, new_name)
					if rename_success:
						changes_made = true
						_show_status("列名已更改为: " + new_name, false)
					else:
						error_messages.append("列名称 '" + new_name + "' 已存在或无效")
				else:
					error_messages.append("无法重命名列")

	# 处理类型更改
	var selected_type = type_option.get_selected_id()
	var current_type = 0
	if json_table and json_table.has_method("get_column_type"):
		current_type = json_table.get_column_type(column_index)

	if selected_type != current_type:
		await _convert_table_column_type(column_index, selected_type, force_convert)
		changes_made = true
		var type_names = [TranslationManager.get_text("string_type"), TranslationManager.get_text("number_type"), TranslationManager.get_text("boolean_type")]
		var type_name = type_names[selected_type] if selected_type < type_names.size() else TranslationManager.get_text("unknown")
		var convert_mode = TranslationManager.get_text("force_conversion") if force_convert else TranslationManager.get_text("smart_conversion")
		_show_status(TranslationManager.get_text("column_conversion_status") % [str(column_index), convert_mode, type_name], false)

	# 显示错误信息
	if not error_messages.is_empty():
		var error_dialog = AcceptDialog.new()
		error_dialog.dialog_text = TranslationManager.get_text("operation_failed") + ":\n" + "\n".join(error_messages)
		error_dialog.title = TranslationManager.get_text("operation_error")

		# 设置按钮文本
		error_dialog.ok_button_text = TranslationManager.get_text("confirm")

		add_child(error_dialog)
		error_dialog.popup_centered()
		error_dialog.confirmed.connect(func(): error_dialog.queue_free())

		# 如果有错误但也有成功的更改，不关闭对话框
		if not changes_made:
			return

	# 关闭对话框
	dialog.queue_free()

	if changes_made:
		_show_status(TranslationManager.get_text("column_edit_completed"), false)

func _on_type_preview_changed(column_index: int, type_option: OptionButton, preview_text: TextEdit, selected_index: int = -1) -> void:
	"""类型选择变化时更新预览"""
	var target_type = selected_index if selected_index >= 0 else type_option.get_selected_id()
	var preview_result = _get_conversion_preview(column_index, target_type)
	preview_text.text = preview_result

func _convert_table_column_type(column_index: int, target_type: int, force_convert: bool = false) -> void:
	# 调用表格组件的类型转换方法
	if json_table and json_table.has_method("convert_column_type"):
		var type_names = [TranslationManager.get_text("string_type"), TranslationManager.get_text("number_type"), TranslationManager.get_text("boolean_type")]
		var type_name = type_names[target_type] if target_type < type_names.size() else TranslationManager.get_text("unknown")
		var convert_mode = TranslationManager.get_text("force_conversion") if force_convert else TranslationManager.get_text("smart_conversion")

		print(TranslationManager.get_text("start_conversion") % [convert_mode, str(column_index), "column_" + str(column_index), type_name])
		await json_table.convert_column_type(column_index, target_type, force_convert)
		print(TranslationManager.get_text("conversion_complete"))

		# 检查转换后的数据
		print("转换后的current_data: ", current_data)

		# 显示状态
		_show_status(TranslationManager.get_text("column_conversion_status") % [str(column_index), convert_mode, type_name], false)
	else:
		_show_status(TranslationManager.get_text("table_component_not_ready"), true)

func _get_conversion_preview(column_index: int, target_type: int) -> String:
	"""获取类型转换预览"""
	if not json_table or not json_table.has_method("get_column_preview"):
		return TranslationManager.get_text("cannot_get_preview")

	return json_table.get_column_preview(column_index, target_type)

func _get_type_name_from_icon(type_icon: String) -> String:
	"""根据类型图标获取类型名称"""
	if " [T]" in type_icon:
		return TranslationManager.get_text("type_string")
	elif " [#]" in type_icon:
		return TranslationManager.get_text("type_number")
	elif " [✓]" in type_icon:
		return TranslationManager.get_text("type_boolean")
	else:
		return TranslationManager.get_text("unknown")

func _add_json_to_tree(data: Variant, parent: TreeItem) -> void:
	match typeof(data):
		TYPE_DICTIONARY:
			for key in data:
				var item = json_tree.create_item(parent)
				item.set_text(0, str(key))
				if typeof(data[key]) in [TYPE_DICTIONARY, TYPE_ARRAY]:
					_add_json_to_tree(data[key], item)
					item.set_metadata(0, {
						"is_container": true,
						"container_data": data[key],
						"parent_data": data,
						"key": key
					})
				else:
					item.set_text(1, str(data[key]))
					item.set_metadata(0, {"key": key, "value": data[key], "parent_data": data})
		TYPE_ARRAY:
			for i in range(data.size()):
				var item = json_tree.create_item(parent)
				item.set_text(0, str(i))
				if typeof(data[i]) in [TYPE_DICTIONARY, TYPE_ARRAY]:
					_add_json_to_tree(data[i], item)
					item.set_metadata(0, {
						"is_container": true,
						"container_data": data[i],
						"parent_data": data,
						"key": i
					})
				else:
					item.set_text(1, str(data[i]))
					item.set_metadata(0, {"key": i, "value": data[i], "parent_data": data})

func _on_tree_item_activated() -> void:
	var selected = json_tree.get_selected()
	if not selected:
		return

	var metadata = selected.get_metadata(0)
	if not metadata:
		return

	if metadata.get("is_container", false):
		current_item = selected
		edit_key.text = str(metadata.get("key", ""))
		edit_value.text = "Container type"
		edit_value.editable = false
		%EditTypeOption.visible = false
		%EditTypeLabel.visible = false
		%AddNewButton.visible = true
		var is_root = metadata["parent_data"] == null
		%DeleteButton.visible = not is_root
		edit_key.editable = not is_root
		edit_dialog.popup_centered()
	else:
		current_item = selected
		edit_key.text = str(metadata["key"])
		edit_value.text = str(metadata["value"])
		edit_value.editable = true
		%EditTypeOption.visible = true
		%EditTypeLabel.visible = true
		%AddNewButton.visible = false
		%DeleteButton.visible = true
		edit_key.editable = true

		var value = metadata["value"]
		match typeof(value):
			TYPE_INT, TYPE_FLOAT:
				%EditTypeOption.select(ValueType.NUMBER)
			TYPE_BOOL:
				%EditTypeOption.select(ValueType.BOOLEAN)
			_:
				%EditTypeOption.select(ValueType.STRING)

		edit_dialog.popup_centered()

func _on_edit_confirm() -> void:
	if not current_item:
		return

	var metadata = current_item.get_metadata(0)
	var parent_data = metadata["parent_data"]
	var old_key = metadata.get("key", "")
	var new_key = edit_key.text

	if metadata.get("is_container", false):
		var container_data = metadata["container_data"]
		if typeof(parent_data) == TYPE_DICTIONARY and old_key != new_key:
			parent_data.erase(old_key)
			parent_data[new_key] = container_data
	else:
		var new_value = edit_value.text
		var selected_type = %EditTypeOption.get_selected_id()

		match selected_type:
			ValueType.STRING:
				new_value = str(new_value)
			ValueType.NUMBER:
				if new_value.is_valid_int():
					new_value = new_value.to_int()
				elif new_value.is_valid_float():
					new_value = new_value.to_float()
				else:
					_show_status("Invalid number format", true)
					return
			ValueType.BOOLEAN:
				if new_value.to_lower() in ["true", "false"]:
					new_value = new_value.to_lower() == "true"
				else:
					_show_status("Invalid boolean value", true)
					return

		if typeof(parent_data) == TYPE_DICTIONARY:
			if old_key != new_key:
				parent_data.erase(old_key)
			parent_data[new_key] = new_value
		elif typeof(parent_data) == TYPE_ARRAY:
			parent_data[old_key] = new_value

	_update_tree_view(current_data)
	json_edit.text = JSON.stringify(current_data, "\t")
	edit_dialog.hide()

func _on_edit_cancel() -> void:
	edit_dialog.hide()

func _on_add_confirm() -> void:
	if not current_item:
		return

	var metadata = current_item.get_metadata(0)
	var container_data = metadata["container_data"]
	var new_key = add_key.text
	var new_value = add_value.text
	var selected_type = type_option.get_selected_id()

	match selected_type:
		ValueType.STRING:
			new_value = str(new_value)
		ValueType.NUMBER:
			if new_value.is_valid_int():
				new_value = new_value.to_int()
			elif new_value.is_valid_float():
				new_value = new_value.to_float()
			else:
				_show_status("Invalid number format", true)
				return
		ValueType.BOOLEAN:
			if new_value.to_lower() in ["true", "false"]:
				new_value = new_value.to_lower() == "true"
			else:
				_show_status("Invalid boolean value", true)
				return
		ValueType.DICTIONARY:
			new_value = {}
		ValueType.ARRAY:
			new_value = []

	if typeof(container_data) == TYPE_DICTIONARY:
		if new_key.is_empty():
			_show_status("Key cannot be empty", true)
			return
		container_data[new_key] = new_value
	elif typeof(container_data) == TYPE_ARRAY:
		container_data.append(new_value)

	if metadata["parent_data"] == null:
		current_data = container_data

	_update_tree_view(current_data)
	json_edit.text = JSON.stringify(current_data, "\t")
	add_dialog.hide()

func _on_add_cancel() -> void:
	add_dialog.hide()

func _on_save_pressed() -> void:
	if current_file_path.is_empty():
		_show_status("Please load or specify a file path first", true)
		return

	var json = JSON.new()
	var error = json.parse(json_edit.text)
	if error != OK:
		_show_status("JSON parse error: " + json.get_error_message(), true)
		return

	if JsonEditorManager.save_json(current_file_path, json.get_data()):
		_show_status("Save successful")
	else:
		_show_status("Save failed", true)

func _on_browse_pressed() -> void:
	file_dialog.popup_centered_ratio(0.7)

func _on_file_selected(path: String) -> void:
	file_path_edit.text = path
	_on_load_pressed()

func _show_status(message: String, is_error: bool = false) -> void:
	status_label.text = message
	status_label.modulate = Color.RED if is_error else Color.GREEN

func _on_delete_pressed() -> void:
	if not current_item:
		return

	var metadata = current_item.get_metadata(0)
	if metadata["parent_data"] == null:
		_show_status("Cannot delete root node", true)
		return

	var parent_data = metadata["parent_data"]
	var key = metadata["key"]

	if typeof(parent_data) == TYPE_DICTIONARY:
		parent_data.erase(key)
	elif typeof(parent_data) == TYPE_ARRAY:
		parent_data.remove_at(key)
		for i in range(key, parent_data.size()):
			var item = json_tree.get_root().find_child(str(i + 1), true, false)
			if item:
				var item_metadata = item.get_metadata(0)
				if item_metadata:
					item_metadata["key"] = i
					item.set_text(0, str(i))

	_update_tree_view(current_data)
	json_edit.text = JSON.stringify(current_data, "\t")
	edit_dialog.hide()

func _on_add_new_pressed() -> void:
	if not current_item:
		return

	var metadata = current_item.get_metadata(0)
	if not metadata.get("is_container", false):
		return

	edit_dialog.hide()
	add_key.text = ""
	add_value.text = ""
	type_option.select(0)
	add_value.editable = true
	add_dialog.popup_centered()

func _on_type_selected(index: int) -> void:
	var type = type_option.get_item_id(index)
	add_value.editable = type not in [ValueType.DICTIONARY, ValueType.ARRAY]
	if not add_value.editable:
		add_value.text = "Container type"
	else:
		add_value.text = ""

func _on_load_pressed() -> void:
	var path = file_path_edit.text
	if path.is_empty():
		_show_status("Please enter a file path", true)
		return

	var data = JsonEditorManager.load_json(path)
	if data == null:
		_show_status("Load failed", true)
		return

	current_data = data
	json_edit.text = JSON.stringify(data, "\t")
	current_file_path = path
	_show_status("Load successful")
	_update_tree_view(data)
	await _update_table_view(data)

func _on_path_text_submitted(new_text : String = ""):
	if new_text != "":
		current_file_path = new_text
		_on_load_pressed()

func _on_chinese_button_pressed() -> void:
	"""切换到中文"""
	_switch_language("zh")

func _on_english_button_pressed() -> void:
	"""切换到英文"""
	_switch_language("en")

func _switch_language(language: String) -> void:
	"""切换语言"""
	current_language = language
	TranslationManager.set_language(language)

	# 更新按钮状态
	_update_language_button_states()

	# 更新界面翻译
	update_translations()

	# 通知表格组件更新翻译
	if json_table and json_table.has_method("update_translations"):
		json_table.update_translations()

	print("语言切换完成，新语言:", language)

func _update_language_button_states() -> void:
	"""更新语言按钮的状态"""
	# 设置按钮的活跃状态
	chinese_button.disabled = (current_language == "zh")
	english_button.disabled = (current_language == "en")

	# 可以添加视觉反馈，比如改变按钮颜色
	if current_language == "zh":
		chinese_button.modulate = Color(0.8, 0.8, 0.8)  # 灰色表示当前选中
		english_button.modulate = Color.WHITE
	else:
		chinese_button.modulate = Color.WHITE
		english_button.modulate = Color(0.8, 0.8, 0.8)
