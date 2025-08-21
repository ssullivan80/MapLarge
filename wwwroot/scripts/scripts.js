function formatBytes(bytes) {
    if (bytes === 0) return '0 B';
    var k = 1024,
        sizes = ['B', 'KB', 'MB', 'GB', 'TB'],
        i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function fetchResults() {
    var path = $("#path").val();
    var search = $("#search").val();
    var recursive = $("#recursive").is(":checked");
    $.get("/search", { path: path, search: search, recursive: recursive }, function (data) {
        populateResults(data);
    }).fail(function (xhr) {
        $("#results").html("<div style='color:red;'>Error: " + xhr.responseText + "</div>");
    });
}

//return results component
function populateResults(data) {
    var summary = `<div>
                            <strong>Directories:</strong> ${data.directoryCount} &nbsp;
                            <strong>Files:</strong> ${data.fileCount} &nbsp;                           
                        </div><hr/>`;

    var html = summary += "<h3>Results</h3>";
    if (data.results.length === 0) {
        html += "<div>No files found.</div>";
    } else {
        html += `<table>
                        <thead>
                            <tr>
                                <th>Name</th>
                                <th>Size</th>                                        
                                <th>Last Modified</th>
                                <th>Folder</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>`;
        data.results.forEach(function (file) {
            html += `<tr>
                            <td>`
                                if (file.type == 'File folder') {
                                    html += `<a href="#" class="dir-link" data-path="${file.fullPath}">${file.name}</a>`;
                                }
                                else {
                                    html += file.name
                                }
                    html += `</td >
                            <td>${file.size > 0 ? formatBytes(file.size) : ''}</td>                            
                            <td>${new Date(file.lastModified).toLocaleString()}</td>
                            <td>${file.folder}</td>
                            <td>`;
                                if (file.type != 'File folder') {
                                    html += `<button class="download-btn" data-path="${file.fullPath}">Download</button>
                                            <button class="delete-btn" data-path="${file.fullPath}">Delete</button>
                                            <button class="move-btn" data-path="${file.fullPath}">Move</button>
                                            <button class="copy-btn" data-path="${file.fullPath}">Copy</button>`;
                                }
                     html += `</td >
                                
                    </tr>`;
        });
        html += "</tbody></table>";
    }
    $("#results").html(html);
}

//document .ready
$(function () {
    fetchResults();


    $("#results").on("click", ".download-btn", function (e) {
        const filePath = $(this).data("path");
        // Create a temporary link to trigger the download
        const link = document.createElement("a");
        link.href = "/download?path=" + encodeURIComponent(filePath);
        link.download = "";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
       
    });

    $("#searchForm").on("submit", function (e) {
        e.preventDefault();
        fetchResults();
    });

    $("#results").on("click", ".dir-link", function (e) {
        e.preventDefault();
        $("#path").val($(this).data("path"));
        fetchResults();
    });

    $("#uploadForm").on("submit", function (e) {
        e.preventDefault();
        var formData = new FormData(this);
        formData.append("path", $("#path").val());
        $.ajax({
            url: "/upload",
            type: "POST",
            data: formData,
            processData: false,
            contentType: false,
            success: function () {
                alert("Upload successful!");
                fetchResults();
            },
            error: function (error) {
                alert("Upload failed: " + error.responseText);
            }
        });
    });

    let currentActionPath = "";

    $("#results").on("click", ".delete-btn", function () {
        if (confirm("Are you sure you want to delete this file?")) {          
            //could have used $.post here but wanted to use correct action
            $.ajax({
                url: "/delete",
                type: "DELETE",
                data: { path: $(this).data("path") },                
                success: function () {                    
                    fetchResults();
                },
                error: function (error) {
                    alert("Delete failed: " + error.responseText);
                }
            });
        }
    });

    $("#results").on("click", ".move-btn", function () {
        currentActionPath = $(this).data("path");
        $("#moveDest").val("");
        $("#moveModal").show();
    });

    $("#results").on("click", ".copy-btn", function () {
        currentActionPath = $(this).data("path");
        $("#copyDest").val("");
        $("#copyModal").show();
    });

    $("#moveConfirm").on("click", function () {
        var dest = $("#moveDest").val();
        $.post("/move", { sourcePath: currentActionPath, destPath: dest }, function () {
            $("#moveModal").hide();
            fetchResults();
        }).fail(function (error) {
            alert("Move failed: " + error.responseText);
        });
    });

    $("#copyConfirm").on("click", function () {
        var dest = $("#copyDest").val();
        $.post("/copy", { sourcePath: currentActionPath, destPath: dest }, function () {
            $("#copyModal").hide();
            fetchResults();
        }).fail(function (error) {
            alert("Copy failed: " + error.responseText);
        });
    });
});