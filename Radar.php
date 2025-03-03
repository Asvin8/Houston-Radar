<?php

use Firebase\JWT\JWT;
use Firebase\JWT\Key;

include_once("Traffic_Data/config.inc.php");
include_once("Traffic_Data/td.inc.php");
include_once("Traffic_Data/ac.inc.php");

//include_once(SMARTY_LIB . '/Smarty.class.php');

header("Content-Security-Policy: script-src 'self' http://api.spectrumtraffic.com");
header("Access-Control-Allow-Origin: *");
header("access-control-allow-credentials: true");
header("access-control-allow-headers: content-type");
header("access-control-allow-methods: GET,HEAD,PUT,PATCH,POST,DELETE");
header("access-control-allow-origin: http://localhost:3001");

//$_POST = json_decode(file_get_contents("php://input"), true);

//Check Which Action To Perform:
if (isset($_POST['act'])) { $act = trim($_POST['act']); }
elseif (isset($_GET['act'])) { $act = trim($_GET['act']); }
else { $act = null; }

function isValidIPv4($ip) { return filter_var($ip, FILTER_VALIDATE_IP, FILTER_FLAG_IPV4) !== false; }

function errorOccured($errorMessage) {
  $resultant['err'] = 1;
  $resultant['message'] = $errorMessage;
  echo json_encode($resultant);
  exit(0);
}

$err = null;

switch ($act) {

  case "get_active_radars":

  $resultant['err'] = 0;

  if($link = dbConnect()){

    $now = time();

    $sql = "SELECT td_equipment.equipment_guid, serial_no, ip_address, td_manifest.count_guid, manifest_guid, latitude, longitude, start_time, FROM_UNIXTIME(start_time) AS start_datetime, end_time,  FROM_UNIXTIME(end_time) AS end_datetime, hours FROM td_equipment
      LEFT JOIN td_manifest ON td_manifest.equipment_guid = td_equipment.equipment_guid
      LEFT JOIN td_recording_schedules ON td_recording_schedules.count_guid = td_manifest.count_guid
      WHERE `type` = 'HOUSTON_SPEEDLANE_PRO' AND start_time IS NOT NULL AND end_time IS NOT NULL AND start_time <= $now AND end_time >= $now";


      $radars = Array();

      if($result = mysqli_query($link, $sql)){

          while($row = mysqli_fetch_assoc($result)){
            $resultant['radars'][] = $row;
          }          

      } else {

        $resultant['message'] = mysqli_errno($link);
        $resultant['err'] = 1;
      }


  } else {
    $err = "Cannot connect to database";
    $resultant['err'] = 1;
  }

  echo json_encode($resultant);
  exit(0);

  break;

  case "get_schedules":

    $resultant['err'] = 0;
    $ip_address = is_null($_GET['ip_address']) ? null : trim($_GET['ip_address']);

    if (!isValidIPv4($ip_address)) {
      errorOccured("Invalid ip address format");
    }

    $start = new DateTime("2025-02-24 12:00:00");
    $end = new DateTime("2025-02-24 13:00:00");

    if (!is_null($ip_address)) {
      $schedules[0]['start'] = $start->format('U');
      $schedules[0]['end'] = $end->format('U');
      $resultant['schedules'] = $schedules;
    } else {
      $resultant['err'] = 1;
      $resultant['message'] = "Invalid IP address";
    }

    echo json_encode($resultant);
    exit(0);

    case "upload_gzip":
      header("Content-Type: text/plain");

      // Check if file was uploaded
      if (!isset($_FILES['file']) || $_FILES['file']['error'] !== 0) {
          errorOccured("No valid file was uploaded.");
      }

      // Validate GZIP file type
      if ($_FILES['file']['type'] !== "application/gzip") {
          errorOccured("Invalid file type: " . $_FILES['file']['type']);
      }

      // Read and decompress GZIP data in memory
      $gzipFilePath = $_FILES['file']['tmp_name'];
      $compressedData = file_get_contents($gzipFilePath);

      if ($compressedData === false) {
          errorOccured("Failed to read uploaded GZIP file.");
      }

      // Decompress GZIP data to get the original CSV content
      $csvContent = gzdecode($compressedData);

      if ($csvContent === false) {
          errorOccured("Failed to decompress GZIP file.");
      } else {

        $filename = ROOT . '/vehicles.csv';
        echo $filename;


        try
        {
          $file = fopen($filename, "w");
          fwrite($file, $csvContent);
          fclose($file);

          //File saved as vehicles.csv.
          $sqlConnect = dbConnect();

          // Insert LOAD DATA INFILE code here
          try {

              // Escape filename for SQL query
              $escapedFilename = mysqli_real_escape_string($sqlConnect, $filename);

              // Build LOAD DATA query
              $sql = "LOAD DATA LOCAL INFILE '$escapedFilename'
                      INTO TABLE asvinTesting
                      FIELDS TERMINATED BY ','
                      ENCLOSED BY '\"'
                      LINES TERMINATED BY '\\n'
                      IGNORE 1 ROWS";

              // Execute query
              if (!mysqli_query($sqlConnect, $sql)) {
                  throw new Exception("MySQL error: " . mysqli_error($sqlConnect));
              }

              debugTrace('CSV data successfully imported into td_speedlane_pro');

          } catch (Exception $e) {
              errorOccured("Data import failed: " . $e->getMessage());
          }


        } catch (Exception $e){
              debugTrace('ERROR: ' . $e->getMessage());
        }
      }

      // Parse CSV content
      $rows = array_map('str_getcsv', explode("\n", trim($csvContent)));

      if (empty($rows) || count($rows) < 2) {
          errorOccured("Invalid CSV format or empty file.");
      }

      // Extract headers and data
      $headers = array_shift($rows);
      $numRecords = count($rows);

      // Generate response with CSV contents
      $response = "File uploaded and processed successfully.\n";
      $response .= "Total records: {$numRecords}\n";
      $response .= "CSV Headers: " . implode(", ", $headers) . "\n";
      $response .= "CSV Data:\n";

      foreach ($rows as $row) {
          $response .= implode(", ", $row) . "\n";
      }

      echo $response;
      exit(0);


  default:
    errorOccured("Unknown method");
}

?>
